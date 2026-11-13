// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Infrastructure;
using OpenPhysical.Middleware.Infrastructure.Trust;
using OpenPhysical.Testing.Piv;

namespace OpenPhysical.Middleware.Tests;

internal static class FingerprintAcquisitionAssertions
{
    internal static async Task RunAsync(FingerprintScenario fixture, bool accepted, string scenario)
    {
        var data = fixture.Reference;
        var proof = CreateProof(data, scenario);
        using var session = new FingerprintReferenceSession(
            data.Authentication.GetEncoded(),
            data.Chuid,
            data.Fingerprint,
            proof,
            scenario is not ("acquisition-pin" or "acquisition-wrong-key")
        );
        if (scenario == "acquisition-reset")
            fixture.Driver.OnCapture = () => session.Current = false;
        fixture.Driver.CancelCapture = scenario == "acquisition-cancel";
        var acquisition = CreateService(fixture);
        var guided = 0;
        var result = await acquisition.VerifyAsync(
            session,
            fixture.Request(Maybe<HolderVerificationEvidence>.None),
            [data.ContentSigner, data.Root],
            fixture.Driver.Device.DeviceId,
            new byte[] { 49, 50, 51, 52, 53, 54 },
            (choices, _) =>
            {
                guided++;
                fixture.Engine.Matches.Should().Be(0);
                choices.Should().Contain(new FingerprintReferenceSelection(2, 0));
                return Task.FromResult(
                    Result.Success<FingerprintReferenceSelection, VerificationError>(new(2, 0))
                );
            },
            CancellationToken.None
        );
        AssertResult(fixture, proof, result, accepted, scenario, guided);
    }

    private static LiveCredentialCardFixture.ProofTransport CreateProof(
        FingerprintReferenceTestData data,
        string scenario
    ) =>
        new(
            true,
            scenario == "acquisition-wrong-key"
                ? data.RootKeys.Private
                : data.AuthenticationKeys.Private,
            KeySlot.CardAuthentication,
            requirePriorAuthentication: true
        );

    private static LiveFingerprintVerificationService CreateService(FingerprintScenario fixture)
    {
        var executor = new PivApduExecutor();
        var pins = new PinCeremonyService(fixture.Authority, executor, fixture.Clock);
        var live = new LiveCredentialVerificationService(
            fixture.Authority,
            pins,
            new CredentialVerificationService(fixture.Authority),
            executor,
            fixture.Clock
        );
        return new(
            live,
            pins,
            fixture.Service,
            CaptureDriverRegistry
                .Create(
                    ImmutableArray.Create<IFingerprintCaptureDriver>(fixture.Driver),
                    fixture.Clock
                )
                .Value,
            new AcquiredFingerprintAdapter(),
            fixture.Clock
        );
    }

    private static void AssertResult(
        FingerprintScenario fixture,
        LiveCredentialCardFixture.ProofTransport proof,
        Result<CredentialVerificationResult, VerificationError> result,
        bool accepted,
        string scenario,
        int guided
    )
    {
        (result.IsSuccess && result.Value.Decision == CredentialVerificationDecision.Accepted)
            .Should()
            .Be(accepted);
        guided
            .Should()
            .Be(scenario is "acquisition-policy-missing" or "acquisition-wrong-key" ? 0 : 1);
        fixture.Engine.Matches.Should().Be(accepted ? 1 : 0);
        proof.VerifyCalls.Should().Be(scenario == "acquisition-pin" ? 1 : 0);
        proof
            .AuthenticationCalls.Should()
            .Be(
                accepted ? 2
                : scenario == "acquisition-policy-missing" ? 0
                : 1
            );
        fixture.Driver.Disposed.Should().Be(fixture.Driver.Opened);
        if (scenario == "acquisition-cancel")
            result.Error.Code.Should().Be(VerificationErrorCodes.Cancelled);
    }
}
