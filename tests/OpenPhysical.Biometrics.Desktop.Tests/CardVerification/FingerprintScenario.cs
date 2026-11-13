// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Infrastructure.Trust;

namespace OpenPhysical.Middleware.Tests;

internal sealed record FingerprintScenario(
    FingerprintReferenceTestData Reference,
    string ProfileId,
    VerifiedBundleSnapshot Bundle,
    FingerprintCaptureTestDriver Driver,
    FingerprintTestClock Clock,
    CredentialCeremonyService Authority,
    FingerprintTestEngine Engine,
    FingerprintCeremonyService Service,
    FingerprintCapture Capture
)
{
    internal CredentialVerificationRequest Request(Maybe<HolderVerificationEvidence> holder) =>
        new(
            VerificationBundleFixture.Now,
            TimeSpan.Zero,
            ProfileId,
            CredentialVerificationWorkflow.Enrolment,
            CredentialSnapshot,
            VerificationBundleFixture.Hash(CredentialSnapshot),
            [],
            holder,
            CredentialStatusState.Unknown,
            Bundle,
            true
        );

    private ImmutableArray<byte> CredentialSnapshot =>
        CredentialEvidenceCodec.Encode(
            ProfileId,
            [
                Reference.Authentication.GetEncoded().ToImmutableArray(),
                Reference.Root.GetEncoded().ToImmutableArray(),
            ]
        );

    internal static async Task<FingerprintScenario> CreateAsync(
        bool corrupt,
        int score,
        string scenario
    )
    {
        var reference = await FingerprintReferenceTestData.CreateAsync(corrupt);
        string profile =
            scenario == "legacy"
                ? CredentialEvidenceCodec.CertificateEnrolmentProfile
                : CredentialEvidenceCodec.CardAuthenticationProfile;
        var driver = new FingerprintCaptureTestDriver(scenario != "acquisition-unqualified");
        var bundle = await FingerprintBundleFixture.CreateAsync(
            profile,
            driver.Device.DeviceId,
            scenario
        );
        var clock = new FingerprintTestClock();
        var authority = new CredentialCeremonyService(clock);
        var engine = new FingerprintTestEngine(score);
        var service = new FingerprintCeremonyService(
            authority,
            new FingerprintComparisonService(engine),
            clock
        );
        var capture = CreateCapture(scenario);
        return new(reference, profile, bundle, driver, clock, authority, engine, service, capture);
    }

    private static FingerprintCapture CreateCapture(string scenario)
    {
        var capturedAt =
            scenario == "capture-before"
                ? VerificationBundleFixture.Now.AddSeconds(-1)
                : VerificationBundleFixture.Now;
        return FingerprintCapture
            .Create(
                new byte[128 * 128],
                128,
                128,
                500,
                FingerPosition.RightIndex,
                capturedAt,
                FingerprintImpression.PlainLiveScan,
                3,
                false
            )
            .Value;
    }

    internal static ImmutableArray<byte> AcquisitionPolicy(string profileId, string deviceId) =>
        FingerprintBundleFixture.AcquisitionPolicy(profileId, deviceId);

    internal Task AssertAsync(bool corrupt, bool accepted, string scenario) =>
        scenario.StartsWith("acquisition", StringComparison.Ordinal)
            ? FingerprintAcquisitionAssertions.RunAsync(this, accepted, scenario)
        : scenario != "legacy"
            ? FingerprintPreparedAssertions.RunAsync(this, corrupt, accepted, scenario)
        : FingerprintLegacyAssertions.RunAsync(this, corrupt, accepted);
}
