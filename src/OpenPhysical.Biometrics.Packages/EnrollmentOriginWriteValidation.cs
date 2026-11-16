// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;
using Org.BouncyCastle.Asn1;

internal static class EnrollmentOriginWriteValidation
{
    internal static bool ValidInput(
        string organizationId,
        string personId,
        string enrollmentId,
        string purpose,
        string stationId,
        ReadOnlySpan<byte> evidence,
        ImmutableArray<PackageArtifact> artifacts,
        IEnrollmentSigner signer
    ) =>
        EnrollmentOriginArtifactCodec.ValidText(organizationId, 256)
        && EnrollmentOriginArtifactCodec.ValidText(personId, 256)
        && EnrollmentOriginArtifactCodec.ValidText(enrollmentId, 256)
        && EnrollmentOriginArtifactCodec.ValidText(purpose, 64)
        && EnrollmentOriginArtifactCodec.ValidText(stationId, 256)
        && evidence.Length <= 1_048_576
        && ArtifactsValid(artifacts)
        && signer.SubjectPublicKeyInfo.Length > 0
        && signer.KeyId.Length == 32
        && EnrollmentOriginArtifactCodec.KeyIdMatches(
            signer.SubjectPublicKeyInfo.Span,
            signer.KeyId.Span
        );

    internal static bool ArtifactsValid(ImmutableArray<PackageArtifact> artifacts)
    {
        if (artifacts.IsDefaultOrEmpty || artifacts.Length > 63)
            return false;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var artifact in artifacts)
        {
            if (
                artifact is null
                || !PackageNames.Valid(artifact.Name)
                || !names.Add(artifact.Name)
                || !EnrollmentOriginArtifactCodec.ValidText(artifact.Role, 128)
                || !EnrollmentOriginArtifactCodec.ValidText(artifact.Format, 128)
                || artifact.Bytes.Length > 67_108_864
            )
                return false;
        }
        return true;
    }

    internal static bool SignedManifestFits(byte[] body, byte[] signature) =>
        new DerSequence(Asn1Object.FromByteArray(body), new DerOctetString(signature))
            .GetEncoded(Asn1Encodable.Der)
            .Length <= 1_048_576;
}
