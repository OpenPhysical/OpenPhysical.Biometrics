// SPDX-License-Identifier: GPL-2.0-or-later
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Infrastructure.Trust;

namespace OpenPhysical.Middleware.Tests;

internal static class FingerprintLegacyAssertions
{
    internal static async Task RunAsync(FingerprintScenario fixture, bool corrupt, bool accepted)
    {
        var data = fixture.Reference;
        var proof = await fixture.Service.CompareAsync(
            fixture.Request(Maybe<HolderVerificationEvidence>.None),
            data.Chuid,
            data.Fingerprint,
            [data.ContentSigner, data.Root],
            fixture.Capture,
            0,
            CancellationToken.None
        );
        proof.IsSuccess.Should().Be(accepted);
        fixture.Engine.Matches.Should().Be(corrupt ? 0 : 1);
        if (!corrupt && !accepted)
            proof.Error.Code.Should().Be(VerificationErrorCodes.HolderMismatch);
        if (!accepted)
            return;
        var evaluator = new CredentialVerificationService(fixture.Authority);
        var result = (
            await evaluator.VerifyAsync(fixture.Request(proof.Value), CancellationToken.None)
        ).Value;
        result.Decision.Should().Be(CredentialVerificationDecision.Accepted);
        result.ValidUntil.Value.Should().Be(VerificationBundleFixture.Now.AddSeconds(60));
        (await evaluator.VerifyAsync(fixture.Request(proof.Value), CancellationToken.None))
            .Value.Holder.State.Should()
            .Be(VerificationCheckState.Missing);
    }
}
