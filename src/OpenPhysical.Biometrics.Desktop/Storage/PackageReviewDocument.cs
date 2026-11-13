// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.Packages;
using OpenPhysical.Biometrics.Piv;

/// <summary>Decoded unsigned objects and verified companions retained only in memory.</summary>
public sealed record PackageReviewDocument
{
    private PackageReviewDocument(
        BiometricPackage package,
        Maybe<FaceRecord> face,
        Maybe<TemplateDocument> fingerprints
    )
    {
        Label = package.Label;
        ExternalId = package.ExternalId;
        Face = face;
        Fingerprints = fingerprints;
        FaceProfileFindings = face.Match(
            PivBiometricProfile.Validate,
            () => ImmutableArray<BiometricProfileDiagnostic>.Empty
        );
        FingerprintProfileFindings = fingerprints.Match(
            value => PivBiometricProfile.Validate(value.Record),
            () => ImmutableArray<BiometricProfileDiagnostic>.Empty
        );
    }

    /// <summary>Gets the shared person label.</summary>
    public string Label { get; }

    /// <summary>Gets the optional person identifier.</summary>
    public Maybe<string> ExternalId { get; }

    /// <summary>Gets the parsed, unmodified facial record.</summary>
    public Maybe<FaceRecord> Face { get; }

    /// <summary>Gets the parsed fingerprint record and verified companion pixels.</summary>
    public Maybe<TemplateDocument> Fingerprints { get; }

    /// <summary>Gets facial PIV metadata findings separately from file validity.</summary>
    public ImmutableArray<BiometricProfileDiagnostic> FaceProfileFindings { get; }

    /// <summary>Gets fingerprint PIV metadata findings separately from file validity.</summary>
    public ImmutableArray<BiometricProfileDiagnostic> FingerprintProfileFindings { get; }

    /// <summary>Inspects package objects without extraction, quality assessment, or plaintext files.</summary>
    /// <param name="package">An opened package with its artifact hashes verified by the package codec.</param>
    /// <param name="token">Cancels companion loading.</param>
    /// <returns>Typed objects with verified associations, or a structural failure.</returns>
    public static Result<PackageReviewDocument, DesktopError> FromPackage(
        BiometricPackage package,
        CancellationToken token = default
    )
    {
        if (token.IsCancellationRequested)
            return new DesktopError("Cancelled", "Package inspection was cancelled.");
        var face = ReadFace(package);
        if (face.IsFailure)
            return face.Error;
        var fingerprints = ReadFingerprints(package, token);
        if (fingerprints.IsFailure)
            return fingerprints.Error;
        if (face.Value.HasNoValue && fingerprints.Value.HasNoValue)
            return new DesktopError(
                "Package.Objects",
                "The package contains no facial or fingerprint object."
            );
        var associated = PackageReviewAssociation.Check(
            package,
            face.Value.HasValue,
            fingerprints.Value.HasValue
        );
        if (associated.IsFailure)
            return associated.Error;
        return new PackageReviewDocument(package, face.Value, fingerprints.Value);
    }

    private static Result<Maybe<FaceRecord>, DesktopError> ReadFace(BiometricPackage package)
    {
        var artifacts = package.Artifacts.Where(value => value.Name == "face.bin").ToArray();
        if (artifacts.Length == 0)
            return Maybe<FaceRecord>.None;
        return Incits385Codec
            .Parse(artifacts[0].Bytes.AsSpan())
            .MapError(error => new DesktopError("Storage.Record", error.Message))
            .Map(value => value.Record)
            .Ensure(
                value => !value.Images.IsDefaultOrEmpty,
                new DesktopError("Storage.Record", "The facial record contains no images.")
            )
            .Map(Maybe.From);
    }

    private static Result<Maybe<TemplateDocument>, DesktopError> ReadFingerprints(
        BiometricPackage package,
        CancellationToken token
    )
    {
        var matching = package.Artifacts.Where(value => value.Name == "fingerprints.fmr").ToArray();
        if (matching.Length == 0)
            return Maybe<TemplateDocument>.None;
        var bytes = matching[0].Bytes.ToArray();
        try
        {
            var parsed = Incits378Codec
                .Parse(bytes)
                .MapError(error => new DesktopError("Storage.Record", error.Message))
                .Map(value => value.Record)
                .Ensure(
                    value => !value.Views.IsDefaultOrEmpty,
                    new DesktopError("Storage.Record", "The fingerprint record contains no views.")
                );
            if (parsed.IsFailure)
                return parsed.Error;
            return TemplateManifestReader
                .LoadFilesAsync(package.Artifacts, bytes, parsed.Value, token)
                .GetAwaiter()
                .GetResult()
                .Map(Maybe.From);
        }
        finally
        {
            Array.Clear(bytes);
        }
    }
}
