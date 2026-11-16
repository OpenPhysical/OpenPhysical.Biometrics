// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages.Tests;

using System.Collections.Immutable;

internal static class EnrollmentEnvelopeExamples
{
    private static readonly DateTimeOffset Created = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    internal static (EnrollmentOrigin Origin, EnrollmentEnvelopeTransfer Transfer) Create(
        EnrollmentEnvelopeTestKeys originSigner,
        EnrollmentEnvelopeTestKeys sender,
        EnrollmentEnvelopeTestKeys recipient,
        bool twoArtifacts = false
    )
    {
        var artifacts = ImmutableArray.CreateBuilder<PackageArtifact>();
        artifacts.Add(
            PackageArtifact
                .Create("face.bin", "public face fixture"u8, "portrait", "image/test")
                .Value
        );
        if (twoArtifacts)
            artifacts.Add(
                PackageArtifact
                    .Create("finger.bin", "public finger fixture"u8, "fingerprint", "image/test")
                    .Value
            );
        var ordered = artifacts.ToImmutable();
        var origin = EnrollmentOrigin
            .Create(
                "org-1",
                "person-1",
                "enrollment-1",
                3,
                "capture",
                "station-1",
                "{\"policy\":\"fixture\"}"u8,
                ordered,
                originSigner
            )
            .Value;
        return (origin, Transfer(origin, ordered));
    }

    internal static EnrollmentEnvelopeTestResolver Resolver(params IEnrollmentSigner[] signers)
    {
        var resolver = new EnrollmentEnvelopeTestResolver();
        foreach (var signer in signers)
            resolver.Add(signer);
        return resolver;
    }

    private static EnrollmentEnvelopeTransfer Transfer(
        EnrollmentOrigin origin,
        ImmutableArray<PackageArtifact> artifacts
    ) =>
        new(
            "org-1",
            "transfer-1",
            "station-1",
            "service-1",
            "enrollment-1",
            3,
            EnrollmentTransferPurpose.Upload,
            Created,
            Created.AddHours(2),
            ["auth:fixture"],
            origin,
            artifacts
        );
}
