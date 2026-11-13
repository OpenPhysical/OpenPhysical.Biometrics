// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure;
using OpenPhysical.Middleware.Infrastructure.Trust;
using OpenPhysical.Testing.Piv;

namespace OpenPhysical.Middleware.Tests;

internal static class TwicAttendedAssertions
{
    internal static async Task<Maybe<HolderVerificationEvidence>> RunAsync(TwicScenario fixture)
    {
        var request = OperatorRequest(fixture);
        if (!fixture.Options.Forged)
        {
            var pin = await new PinCeremonyService(
                fixture.Authority,
                new PivApduExecutor(),
                fixture.Clock
            ).VerifyAsync(
                request,
                OperatorCard(fixture),
                new byte[] { 49, 50, 51, 52, 53, 54 },
                CancellationToken.None
            );
            pin.IsSuccess.Should().BeTrue(pin.IsFailure ? pin.Error.Operation : string.Empty);
            request = pin.Value;
        }
        var confirmation = await fixture.Fingerprints.ConfirmTwicExceptionAsync(
            fixture.Bare,
            fixture.Policy.Profile,
            new(fixture.Reference.Key, TwicPrivacyKeyContainer.AlgorithmAesEcb, 0),
            request,
            CancellationToken.None
        );
        confirmation
            .IsSuccess.Should()
            .Be(
                !fixture.Options.Forged,
                confirmation.IsFailure ? confirmation.Error.Operation : string.Empty
            );
        fixture.Engine.Matches.Should().Be(0);
        return confirmation.IsSuccess ? confirmation.Value : Maybe<HolderVerificationEvidence>.None;
    }

    private static CredentialVerificationRequest OperatorRequest(TwicScenario fixture)
    {
        var snapshot = CredentialEvidenceCodec.Encode(
            CredentialEvidenceCodec.PinAuthenticationProfile,
            [
                fixture.Reference.OperatorCertificate.GetEncoded().ToImmutableArray(),
                fixture.Reference.Certificates.Root.GetEncoded().ToImmutableArray(),
            ]
        );
        return new(
            VerificationBundleFixture.Now,
            TimeSpan.Zero,
            CredentialEvidenceCodec.PinAuthenticationProfile,
            CredentialVerificationWorkflow.LocalVerification,
            snapshot,
            VerificationBundleFixture.Hash(snapshot),
            [],
            Maybe<HolderVerificationEvidence>.None,
            CredentialStatusState.Unknown,
            fixture.Policy.Bundle,
            true
        );
    }

    private static LiveCredentialCardFixture.Card OperatorCard(TwicScenario fixture)
    {
        var data = fixture.Reference;
        var cardAuth = CeremonyCertificateFixture
            .PinCertificate(
                data.OperatorKeys.Public,
                data.Certificates.RootKeys.Private,
                false,
                leafSerial: 5,
                cardAuth: true
            )
            .GetEncoded();
        return new(
            new LiveCredentialCardFixture.ProofTransport(
                true,
                data.OperatorKeys.Private,
                cardAuthCertificate: cardAuth,
                requirePriorAuthentication: true
            )
        );
    }
}
