// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure.Trust;
using OpenPhysical.Middleware.Infrastructure.Twic;

namespace OpenPhysical.Middleware.Tests;

internal static class TwicAcceptanceAssertions
{
    internal static async Task RunAsync(TwicScenarioOptions options)
    {
        var fixture = await TwicScenario.CreateAsync(options);
        if (options.Path.StartsWith("live", StringComparison.Ordinal))
        {
            await TwicLiveAssertions.RunAsync(fixture);
            return;
        }
        Maybe<HolderVerificationEvidence> holder = new HolderVerificationEvidence(
            fixture.Bare.CredentialSha256,
            HolderVerificationMethod.Biometric,
            VerificationBundleFixture.Now,
            new string('c', 64)
        );
        if (options.Attended)
        {
            var confirmed = await TwicAttendedAssertions.RunAsync(fixture);
            if (confirmed.HasValue)
                holder = confirmed;
        }
        else if (!options.Forged)
        {
            holder = await TwicPreparedAssertions.RunAsync(fixture);
            if (holder.HasNoValue)
                return;
        }
        await AssertFinalAsync(fixture, holder);
    }

    private static async Task AssertFinalAsync(
        TwicScenario fixture,
        Maybe<HolderVerificationEvidence> holder
    )
    {
        var request = fixture.WithHolder(holder);
        var result = await new TwicAcceptanceService(fixture.Authority).VerifyAsync(
            request,
            fixture.Policy.Profile,
            Evidence(fixture, request, holder),
            CancellationToken.None
        );
        result.IsSuccess.Should().BeTrue();
        result
            .Value.Decision.Should()
            .Be(
                fixture.Options.Cancelled ? CredentialVerificationDecision.Rejected
                : fixture.Options.Forged ? CredentialVerificationDecision.InsufficientEvidence
                : CredentialVerificationDecision.Accepted
            );
    }

    private static TwicAcceptanceEvidence Evidence(
        TwicScenario fixture,
        CredentialVerificationRequest request,
        Maybe<HolderVerificationEvidence> holder
    )
    {
        var passed = new VerificationCheck(true, VerificationCheckState.Passed, []);
        return new(
            request.CredentialSha256,
            TwicPolicyFixture.ProfileId,
            1,
            fixture.Options.Generation,
            fixture.Policy.Profile.Workflow,
            1,
            VerificationBundleFixture.Now,
            [VerificationBundleFixture.Hash(fixture.Reference.Chuid)],
            passed,
            passed,
            CredentialStatusState.Good,
            fixture.Policy.Bundle.StatusEvidence.Single(value =>
                value.SourceKind == StatusEvidenceKind.TwicCancellationList
            ),
            holder,
            Maybe<string>.None
        );
    }
}
