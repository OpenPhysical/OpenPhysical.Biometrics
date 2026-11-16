// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class EnrollmentOriginCodec
{
    internal sealed record ArtifactInfo(
        string Name,
        string Role,
        string MediaType,
        int Length,
        byte[] Hash
    );

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
    ) =>
        EnrollmentOriginWriter.Create(
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

    internal static Result<EnrollmentOrigin, PackageError> Read(
        byte[] encoded,
        IEnrollmentKeyResolver resolver
    ) => EnrollmentOriginReader.Read(encoded, resolver);
}
