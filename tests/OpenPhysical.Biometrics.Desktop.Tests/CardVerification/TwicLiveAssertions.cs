// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure;
using OpenPhysical.Middleware.Infrastructure.Trust;
using OpenPhysical.Middleware.Infrastructure.Twic;
using OpenPhysical.Middleware.Twic.Codec;
using OpenPhysical.Testing.Piv;

namespace OpenPhysical.Middleware.Tests;

internal static class TwicLiveAssertions
{
    internal static async Task RunAsync(TwicScenario fixture)
    {
        var path = fixture.Options.Path;
        var data = fixture.Reference;
        var proof = new LiveCredentialCardFixture.ProofTransport(
            true,
            path == "live-wrong-key"
                ? data.Certificates.RootKeys.Private
                : data.Certificates.AuthenticationKeys.Private,
            KeySlot.CardAuthentication
        );
        using var session = Session(fixture, proof);
        if (path == "live-reset")
            fixture.Driver.OnCapture = () => session.Current = false;
        fixture.Driver.CancelCapture = path == "live-cancel";
        var guided = 0;
        var result = await Service(fixture)
            .VerifyAsync(
                session,
                fixture.Bare,
                fixture.Policy.Profile,
                1,
                PrivacyKey(fixture),
                fixture.Driver.Device.DeviceId,
                Guide(fixture, () => guided++),
                CancellationToken.None
            );
        AssertResult(fixture, result, session, proof, guided);
    }

    private static TwicReferenceSession Session(
        TwicScenario fixture,
        LiveCredentialCardFixture.ProofTransport proof
    ) =>
        new(
            fixture.Options.Generation,
            fixture.Reference.Chuid,
            fixture.Reference.Encrypted,
            fixture.Reference.Certificates.Authentication.GetEncoded().ToImmutableArray(),
            proof,
            TwicPrivacyKeyContainerCodec
                .ToBytes(new(fixture.Reference.Key, TwicPrivacyKeyContainer.AlgorithmAesEcb, 0))
                .Value,
            fixture.Options.Path == "live-contactless-key-missing"
        );

    private static Maybe<TwicPrivacyKeyContainer> PrivacyKey(TwicScenario fixture) =>
        fixture.Options.Path is "live-contact-key" or "live-contactless-key-missing"
            ? Maybe<TwicPrivacyKeyContainer>.None
            : new TwicPrivacyKeyContainer(
                fixture.Reference.Key,
                TwicPrivacyKeyContainer.AlgorithmAesEcb,
                0
            );

    private static LiveTwicFingerprintVerificationService Service(TwicScenario fixture) =>
        new(
            fixture.Authority,
            new TwicAcceptanceService(fixture.Authority),
            fixture.Fingerprints,
            CaptureDriverRegistry
                .Create(
                    ImmutableArray.Create<IFingerprintCaptureDriver>(fixture.Driver),
                    fixture.Clock
                )
                .Value,
            new AcquiredFingerprintAdapter(),
            new PivApduExecutor(),
            fixture.Clock
        );

    private static Func<
        ImmutableArray<FingerprintReferenceSelection>,
        CancellationToken,
        Task<Result<FingerprintReferenceSelection, VerificationError>>
    > Guide(TwicScenario fixture, Action guided) =>
        (choices, _) =>
        {
            guided();
            fixture.Engine.Matches.Should().Be(0);
            choices.Should().Equal(new[] { new FingerprintReferenceSelection(2, 0) });
            return Task.FromResult(
                Result.Success<FingerprintReferenceSelection, VerificationError>(new(2, 0))
            );
        };

    private static void AssertResult(
        TwicScenario fixture,
        Result<CredentialVerificationResult, VerificationError> result,
        TwicReferenceSession session,
        LiveCredentialCardFixture.ProofTransport proof,
        int guided
    )
    {
        var path = fixture.Options.Path;
        var cancelled = fixture.Options.Cancelled;
        var expected = !cancelled && path is "live" or "live-no-card-proof" or "live-contact-key";
        (result.IsSuccess && result.Value.Decision == CredentialVerificationDecision.Accepted)
            .Should()
            .Be(
                expected,
                result.IsFailure ? result.Error.Operation : result.Value.Decision.ToString()
            );
        guided
            .Should()
            .Be(cancelled || path is "live-wrong-key" or "live-contactless-key-missing" ? 0 : 1);
        session
            .ProofCommands.Should()
            .Be(
                cancelled || path == "live-no-card-proof" ? 0
                : expected ? 2
                : 1
            );
        proof.VerifyCalls.Should().Be(0);
        fixture.Driver.Disposed.Should().Be(fixture.Driver.Opened);
        if (path == "live-cancel")
            result.Error.Code.Should().Be(VerificationErrorCodes.Cancelled);
    }
}
