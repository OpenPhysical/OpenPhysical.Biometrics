// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using System.Text.Json;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure.Trust;
using OpenPhysical.Middleware.Infrastructure.Twic;

namespace OpenPhysical.Middleware.Tests;

internal sealed record TwicPolicyFixture(
    TwicAcceptanceProfile Profile,
    VerifiedBundleSnapshot Bundle
)
{
    internal const string ProfileId = "twic.fixture.v1";

    internal static async Task<TwicPolicyFixture> CreateAsync(
        TwicReferenceTestData data,
        TwicScenarioOptions options,
        FingerprintCaptureTestDriver driver
    )
    {
        var profile = ProfileFor(options);
        var inputs = Inputs(data, options, profile);
        if (options.Path.StartsWith("live", StringComparison.Ordinal))
            inputs = inputs.Add(
                "acquisition",
                BundleArtifactKind.VerificationProfile,
                "openphysical.fingerprint-acquisition-policy",
                FingerprintBundleFixture.AcquisitionPolicy(ProfileId, driver.Device.DeviceId)
            );
        if (options.Attended)
            inputs = TwicAttendedBundleFixture.Add(inputs, data);
        var bundle = await VerifyBundleAsync(inputs, options.Attended);
        var resolved = TwicAcceptanceService.ReadProfile(bundle, ProfileId);
        resolved.IsSuccess.Should().BeTrue();
        TwicAcceptanceService
            .EncodeProfile(resolved.Value)
            .Should()
            .Equal(TwicAcceptanceService.EncodeProfile(profile));
        TwicAcceptanceService
            .ReadProfile(bundle, "unconfigured.profile")
            .IsFailure.Should()
            .BeTrue();
        return new(profile, bundle);
    }

    private static TwicAcceptanceProfile ProfileFor(TwicScenarioOptions options) =>
        new(
            ProfileId,
            1,
            options.Generation,
            TwicAcceptanceWorkflow.InitialLinkPreparation,
            "fixture-explicit-bounds",
            options.Path != "live-no-card-proof",
            true,
            options.Attended,
            ImmutableDictionary<int, TimeSpan>
                .Empty.Add(1, TimeSpan.FromHours(1))
                .Add(2, TimeSpan.FromMinutes(30))
                .Add(3, TimeSpan.FromMinutes(10))
        );

    private static TwicBundleInputs Inputs(
        TwicReferenceTestData data,
        TwicScenarioOptions options,
        TwicAcceptanceProfile profile
    )
    {
        var policy = TwicAcceptanceService.EncodeProfile(profile);
        var matching = FingerprintBundleFixture.MatchingPolicy(ProfileId, "fixture-match");
        var ccl = CancellationBytes(data.FascN, options.Cancelled);
        return CancellationInputs(ccl)
            .Add(
                "twic-policy",
                BundleArtifactKind.VerificationProfile,
                TwicAcceptanceService.PolicyProfileId,
                policy
            )
            .Add(
                "matching",
                BundleArtifactKind.VerificationProfile,
                "openphysical.fingerprint-matching-policy",
                matching
            )
            .Add(
                "ccl",
                BundleArtifactKind.StatusEvidence,
                TwicCancellationEvidenceReader.ProfileId,
                ccl
            );
    }

    private static TwicBundleInputs CancellationInputs(ImmutableArray<byte> ccl)
    {
        var artifact = TwicBundleInputs.Artifact(
            "ccl",
            BundleArtifactKind.StatusEvidence,
            TwicCancellationEvidenceReader.ProfileId,
            ccl
        );
        return new(
            [],
            ImmutableDictionary<string, ImmutableArray<byte>>.Empty,
            [CancellationStatus(artifact)],
            [ProfileId]
        );
    }

    private static StatusEvidenceDescriptor CancellationStatus(BundleArtifactDescriptor artifact)
    {
        var now = VerificationBundleFixture.Now;
        return new(
            artifact,
            StatusEvidenceKind.TwicCancellationList,
            VerificationBundleVerifier.KeyId(VerificationBundleFixture.SharedSigner.Certificate),
            Maybe<string>.None,
            now.AddMinutes(-1),
            now.AddMinutes(-1),
            now.AddHours(1),
            TwicCancellationEvidenceReader.SourceAuthenticationProfile,
            Maybe<TimeSpan>.None
        );
    }

    private static ImmutableArray<byte> CancellationBytes(byte[] fascn, bool cancelled) =>
        JsonSerializer
            .SerializeToUtf8Bytes(
                new
                {
                    schemaVersion = 1,
                    sourceUri = "https://universalenroll.dhs.gov/ccl/CCL.CSV",
                    sourceSha256 = new string('b', 64),
                    cancelledFascN = new[]
                    {
                        cancelled ? Convert.ToHexStringLower(fascn) : new string('a', 50),
                    },
                }
            )
            .ToImmutableArray();

    private static async Task<VerifiedBundleSnapshot> VerifyBundleAsync(
        TwicBundleInputs inputs,
        bool attended
    )
    {
        var policy = new VerificationPolicySnapshot(
            "test-policy",
            1,
            TimeSpan.FromHours(24),
            TimeSpan.FromSeconds(60),
            true,
            attended,
            attended,
            inputs.Profiles
        );
        return (
            await new VerificationBundleFixture(
                extra: inputs.Artifacts,
                content: inputs.Contents,
                statuses: inputs.Statuses,
                policyOverride: policy
            ).Verify()
        ).Value;
    }
}
