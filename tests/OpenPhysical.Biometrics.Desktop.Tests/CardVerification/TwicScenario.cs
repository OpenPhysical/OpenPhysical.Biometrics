// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Infrastructure.Trust;
using OpenPhysical.Middleware.Infrastructure.Twic;
using Org.BouncyCastle.Crypto.Engines;

namespace OpenPhysical.Middleware.Tests;

internal sealed record TwicScenario(
    TwicScenarioOptions Options,
    TwicReferenceTestData Reference,
    TwicPolicyFixture Policy,
    FingerprintCaptureTestDriver Driver,
    FingerprintTestClock Clock,
    CredentialCeremonyService Authority,
    FingerprintTestEngine Engine,
    FingerprintCeremonyService Fingerprints,
    FingerprintCapture Capture,
    CredentialVerificationRequest Bare,
    Guid Proof
)
{
    internal static async Task<TwicScenario> CreateAsync(TwicScenarioOptions options)
    {
        var data = await TwicReferenceTestData.CreateAsync(options.Generation, options.Attended);
        var driver = new FingerprintCaptureTestDriver(true);
        var policy = await TwicPolicyFixture.CreateAsync(data, options, driver);
        var clock = new FingerprintTestClock();
        var authority = new CredentialCeremonyService(clock);
        var engine = new FingerprintTestEngine(100);
        var fingerprints = new FingerprintCeremonyService(
            authority,
            new FingerprintComparisonService(engine),
            clock
        );
        var bare = BareRequest(data, policy, options);
        return new(
            options,
            data,
            policy,
            driver,
            clock,
            authority,
            engine,
            fingerprints,
            CaptureFrame(),
            bare,
            CardProof(authority, data, bare)
        );
    }

    private static CredentialVerificationRequest BareRequest(
        TwicReferenceTestData data,
        TwicPolicyFixture policy,
        TwicScenarioOptions options
    )
    {
        var snapshot = TwicCredentialEvidenceCodec.Encode(
            TwicPolicyFixture.ProfileId,
            options.Generation,
            data.Chuid,
            [
                data.Certificates.Authentication.GetEncoded().ToImmutableArray(),
                data.Certificates.Root.GetEncoded().ToImmutableArray(),
            ],
            [
                data.Certificates.Content.GetEncoded().ToImmutableArray(),
                data.Certificates.Root.GetEncoded().ToImmutableArray(),
            ],
            data.Encrypted
        );
        return new(
            VerificationBundleFixture.Now,
            TimeSpan.Zero,
            TwicPolicyFixture.ProfileId,
            CredentialVerificationWorkflow.Twic,
            snapshot,
            VerificationBundleFixture.Hash(snapshot),
            [],
            Maybe<HolderVerificationEvidence>.None,
            CredentialStatusState.Unknown,
            policy.Bundle,
            true
        );
    }

    private static Guid CardProof(
        CredentialCeremonyService authority,
        TwicReferenceTestData data,
        CredentialVerificationRequest request
    )
    {
        var challenge = authority
            .Begin(
                request,
                data.Certificates.Authentication.GetEncoded().ToImmutableArray(),
                KeySlot.CardAuthentication
            )
            .Value;
        var rsa = new RsaEngine();
        rsa.Init(false, data.Certificates.AuthenticationKeys.Private);
        var bytes = challenge.Challenge.ToArray();
        return authority
            .Complete(
                challenge.Id,
                VerificationBundleFixture.RsaResponse(rsa.ProcessBlock(bytes, 0, bytes.Length))
            )
            .Value;
    }

    private static FingerprintCapture CaptureFrame() =>
        FingerprintCapture
            .Create(
                new byte[128 * 128],
                128,
                128,
                500,
                FingerPosition.RightIndex,
                VerificationBundleFixture.Now,
                FingerprintImpression.PlainLiveScan,
                3,
                false
            )
            .Value;

    internal CredentialVerificationRequest WithHolder(Maybe<HolderVerificationEvidence> holder) =>
        new(
            Bare.EvaluatedAt,
            Bare.ClockUncertainty,
            Bare.ProfileId,
            Bare.Workflow,
            Bare.CredentialSnapshot,
            Bare.CredentialSha256,
            Proof.ToByteArray(bigEndian: true).ToImmutableArray(),
            holder,
            Bare.Lifecycle,
            Bare.Bundle,
            Bare.Offline
        );
}
