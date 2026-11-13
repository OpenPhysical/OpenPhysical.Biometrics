// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Associates immutable unsigned objects with one person and caller-owned acquisition evidence.</summary>
public sealed record BiometricPackage
{
    private BiometricPackage(
        string personId,
        string label,
        Maybe<string> externalId,
        ImmutableArray<byte> evidenceJson,
        ImmutableArray<PackageArtifact> artifacts
    )
    {
        PersonId = personId;
        Label = label;
        ExternalId = externalId;
        EvidenceJson = evidenceJson;
        Artifacts = artifacts;
    }

    /// <summary>Gets the stable opaque person-session identifier.</summary>
    public string PersonId { get; }

    /// <summary>Gets the operator's person label, stored only inside encryption.</summary>
    public string Label { get; }

    /// <summary>Gets an optional external association, stored only inside encryption.</summary>
    public Maybe<string> ExternalId { get; }

    /// <summary>Gets the UTF-8 JSON object containing capture plan, reviews, versions, and provenance.</summary>
    public ImmutableArray<byte> EvidenceJson { get; }

    /// <summary>Gets artifacts in the caller's declared order.</summary>
    public ImmutableArray<PackageArtifact> Artifacts { get; }

    /// <summary>Creates a complete unsigned staging package; it makes no identity or PIV issuance attestation.</summary>
    /// <param name="personId">Stable opaque person-session identifier.</param>
    /// <param name="label">Operator-facing label.</param>
    /// <param name="externalId">Optional external person identifier.</param>
    /// <param name="evidenceJson">Owned JSON object bytes.</param>
    /// <param name="artifacts">Unsigned codec outputs and explicitly selected companions.</param>
    /// <returns>An immutable package or a metadata/artifact validation failure.</returns>
    public static Result<BiometricPackage, PackageError> Create(
        string personId,
        string label,
        Maybe<string> externalId,
        ReadOnlySpan<byte> evidenceJson,
        ImmutableArray<PackageArtifact> artifacts
    )
    {
        var identity = ValidateIdentity(personId, label, externalId);
        if (identity.IsFailure)
            return identity.Error;
        if (
            artifacts.IsDefaultOrEmpty
            || artifacts
                .Select(value => value.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != artifacts.Length
        )
            return new PackageError(
                PackageErrorCodes.InvalidArtifact,
                "Include at least one artifact with unique portable names."
            );
        var evidence = PackageJson.Validate(evidenceJson);
        return evidence.IsFailure
            ? evidence.Error
            : new BiometricPackage(
                personId,
                label,
                externalId,
                ImmutableArray.Create(evidenceJson),
                artifacts
            );
    }

    private static UnitResult<PackageError> ValidateIdentity(
        string personId,
        string label,
        Maybe<string> externalId
    ) =>
        !string.IsNullOrWhiteSpace(personId)
        && personId.Length <= 256
        && !string.IsNullOrWhiteSpace(label)
        && label.Length <= 512
        && externalId.Match(
            value => !string.IsNullOrWhiteSpace(value) && value.Length <= 256,
            () => true
        )
            ? UnitResult.Success<PackageError>()
            : new PackageError(
                PackageErrorCodes.InvalidManifest,
                "Provide a person identifier and label within their supported limits."
            );
}
