// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Infrastructure.Trust;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.X509;

namespace OpenPhysical.Middleware.Tests;

internal static class TwicAttendedBundleFixture
{
    internal static TwicBundleInputs Add(TwicBundleInputs inputs, TwicReferenceTestData data) =>
        AddCrl(AddOperators(inputs, data), data) with
        {
            Profiles = inputs.Profiles.Add(CredentialEvidenceCodec.PinAuthenticationProfile),
        };

    private static TwicBundleInputs AddOperators(
        TwicBundleInputs inputs,
        TwicReferenceTestData data
    )
    {
        var roots = CredentialEvidenceCodec.EncodeTrustStore([
            data.Certificates.Root.GetEncoded().ToImmutableArray(),
        ]);
        var operators = JsonSerializer
            .SerializeToUtf8Bytes(
                new
                {
                    schemaVersion = 1,
                    operatorCertificateSha256 = new[]
                    {
                        VerificationBundleFixture.Hash(
                            data.OperatorCertificate.GetEncoded().ToImmutableArray()
                        ),
                    },
                }
            )
            .ToImmutableArray();
        return inputs
            .Add("anchors", BundleArtifactKind.TrustStore, "openphysical.x509-trust-store", roots)
            .Add(
                "operators",
                BundleArtifactKind.VerificationProfile,
                "openphysical.twic-attended-operators",
                operators
            );
    }

    private static TwicBundleInputs AddCrl(TwicBundleInputs inputs, TwicReferenceTestData data)
    {
        var bytes = Crl(data);
        var artifact = new BundleArtifactDescriptor(
            "crl",
            BundleArtifactKind.StatusEvidence,
            "application/pkix-crl",
            bytes.Length,
            VerificationBundleFixture.Hash(bytes),
            "x509.direct-crl.v1",
            1
        );
        return inputs with
        {
            Artifacts = inputs.Artifacts.Add(artifact),
            Contents = inputs.Contents.Add("crl", bytes),
            Statuses = inputs.Statuses.Add(Status(artifact, data)),
        };
    }

    private static StatusEvidenceDescriptor Status(
        BundleArtifactDescriptor artifact,
        TwicReferenceTestData data
    )
    {
        var now = VerificationBundleFixture.Now;
        return new(
            artifact,
            StatusEvidenceKind.Crl,
            VerificationBundleVerifier.KeyId(data.Certificates.Root),
            Maybe<string>.None,
            now.AddMinutes(-1),
            now.AddMinutes(-1),
            now.AddHours(1),
            "x509.direct-crl.v1",
            Maybe<TimeSpan>.None
        );
    }

    private static ImmutableArray<byte> Crl(TwicReferenceTestData data)
    {
        var generator = new X509V2CrlGenerator();
        generator.SetIssuerDN(data.Certificates.Root.SubjectDN);
        generator.SetThisUpdate(VerificationBundleFixture.Now.AddMinutes(-1).UtcDateTime);
        generator.SetNextUpdate(VerificationBundleFixture.Now.AddHours(1).UtcDateTime);
        return generator
            .Generate(new Asn1SignatureFactory("SHA256WITHRSA", data.Certificates.RootKeys.Private))
            .GetEncoded()
            .ToImmutableArray();
    }
}
