// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class EnrollmentOriginWriter
{
    internal static Result<EnrollmentOrigin, PackageError> Create(
        string organizationId,
        string personId,
        string enrollmentId,
        uint revision,
        string purpose,
        string stationId,
        ReadOnlySpan<byte> evidence,
        ImmutableArray<PackageArtifact> artifacts,
        IEnrollmentSigner signer
    )
    {
        if (
            !EnrollmentOriginWriteValidation.ValidInput(
                organizationId,
                personId,
                enrollmentId,
                purpose,
                stationId,
                evidence,
                artifacts,
                signer
            )
        )
            return EnrollmentOriginArtifactCodec.Invalid(
                "Origin metadata, evidence, artifacts, or signing key is invalid."
            );
        return BuildOrigin(
            organizationId,
            personId,
            enrollmentId,
            revision,
            purpose,
            stationId,
            evidence,
            artifacts,
            signer
        );
    }

    private static Result<EnrollmentOrigin, PackageError> BuildOrigin(
        string organizationId,
        string personId,
        string enrollmentId,
        uint revision,
        string purpose,
        string stationId,
        ReadOnlySpan<byte> evidence,
        ImmutableArray<PackageArtifact> artifacts,
        IEnrollmentSigner signer
    )
    {
        var evidenceBytes = evidence.ToArray();
        var manifest = WriteManifest(
            organizationId,
            personId,
            enrollmentId,
            revision,
            purpose,
            stationId,
            evidenceBytes,
            artifacts,
            signer
        );
        if (manifest.IsFailure || manifest.Value.Length > 1_048_576)
            return manifest.IsFailure
                ? manifest.Error
                : EnrollmentOriginArtifactCodec.Limit("The origin manifest exceeds 1 MiB.");
        return CreateSignedOrigin(
            organizationId,
            personId,
            enrollmentId,
            revision,
            purpose,
            stationId,
            evidence,
            manifest.Value,
            signer
        );
    }

    private static Result<EnrollmentOrigin, PackageError> CreateSignedOrigin(
        string organizationId,
        string personId,
        string enrollmentId,
        uint revision,
        string purpose,
        string stationId,
        ReadOnlySpan<byte> evidence,
        byte[] manifest,
        IEnrollmentSigner signer
    )
    {
        var signature = SignManifest(manifest, signer);
        if (signature.IsFailure)
            return signature.Error;
        return EnrollmentOrigin.FromParsed(
            organizationId,
            personId,
            enrollmentId,
            revision,
            purpose,
            stationId,
            evidence.ToArray(),
            manifest,
            signature.Value.ToArray(),
            signer.KeyId.ToArray()
        );
    }

    private static Result<byte[], PackageError> WriteManifest(
        string organizationId,
        string personId,
        string enrollmentId,
        uint revision,
        string purpose,
        string stationId,
        byte[] evidence,
        ImmutableArray<PackageArtifact> artifacts,
        IEnrollmentSigner signer
    ) =>
        PackageEdge.Try(
            () =>
                EnrollmentOriginArtifactCodec.WriteBody(
                    organizationId,
                    personId,
                    enrollmentId,
                    revision,
                    purpose,
                    stationId,
                    evidence,
                    artifacts,
                    signer.KeyId.ToArray()
                ),
            PackageErrorCodes.InvalidTransfer,
            "The origin manifest could not be encoded."
        );

    private static Result<byte[], PackageError> SignManifest(
        byte[] manifest,
        IEnrollmentSigner signer
    )
    {
        var digest = EnrollmentOriginArtifactCodec.SignatureDigest(manifest);
        var signature = PackageEdge.Bind(
            () => signer.SignDigest(digest),
            PackageErrorCodes.CryptoFailed,
            "The origin signing device could not sign the manifest."
        );
        if (signature.IsFailure || signature.Value.Length == 0)
        {
            Array.Clear(digest);
            return signature.IsFailure
                ? signature.Error
                : EnrollmentOriginArtifactCodec.Invalid("The origin signature is empty.");
        }
        return VerifySignature(
            manifest,
            digest,
            signature.Value,
            signer.SubjectPublicKeyInfo.ToArray()
        );
    }

    private static Result<byte[], PackageError> VerifySignature(
        byte[] manifest,
        byte[] digest,
        byte[] signature,
        byte[] subjectPublicKeyInfo
    )
    {
        var verified = PackageEdge.Try(
            () => EnrollmentEnvelopeCrypto.Verify(subjectPublicKeyInfo, digest, signature),
            PackageErrorCodes.CryptoFailed,
            "The origin signing key is invalid."
        );
        Array.Clear(digest);
        if (verified.IsFailure || !verified.Value)
            return new PackageError(
                PackageErrorCodes.SignatureInvalid,
                "The origin signing device returned an invalid P-256 signature."
            );
        return EnrollmentOriginWriteValidation.SignedManifestFits(manifest, signature)
            ? signature
            : EnrollmentOriginArtifactCodec.Limit("The signed origin manifest exceeds 1 MiB.");
    }
}
