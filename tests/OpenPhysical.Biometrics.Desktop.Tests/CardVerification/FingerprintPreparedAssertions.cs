// SPDX-License-Identifier: GPL-2.0-or-later
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Infrastructure.Trust;

namespace OpenPhysical.Middleware.Tests;

internal static class FingerprintPreparedAssertions
{
    internal static async Task RunAsync(
        FingerprintScenario fixture,
        bool corrupt,
        bool accepted,
        string scenario
    )
    {
        var data = fixture.Reference;
        using var session = CreateSession(data);
        var request = fixture.Request(Maybe<HolderVerificationEvidence>.None);
        var prepared = await fixture.Service.PrepareAsync(
            request,
            session,
            [data.ContentSigner, data.Root],
            CancellationToken.None
        );
        fixture
            .Engine.Matches.Should()
            .Be(0, "preparation authenticates without capture or comparison");
        prepared.IsSuccess.Should().Be(!corrupt);
        if (prepared.IsFailure)
            return;
        prepared
            .Value.Selections.Should()
            .BeEquivalentTo(
                new[]
                {
                    new FingerprintReferenceSelection(2, 0),
                    new FingerprintReferenceSelection(7, 0),
                }
            );
        if (scenario == "reset")
            session.Current = false;
        if (scenario == "expiry")
            fixture.Clock.Now = prepared.Value.ExpiresAt;
        await CompareAsync(fixture, request, session, prepared.Value, accepted, scenario);
    }

    private static async Task CompareAsync(
        FingerprintScenario fixture,
        CredentialVerificationRequest request,
        FingerprintReferenceSession session,
        PreparedFingerprintReference prepared,
        bool accepted,
        string scenario
    )
    {
        var data = fixture.Reference;
        using var other = CreateSession(data);
        var compared = await UsedService(fixture, scenario)
            .ComparePreparedAsync(
                prepared,
                UsedRequest(request, scenario),
                scenario == "session" ? other : session,
                fixture.Capture,
                0,
                CancellationToken.None
            );
        compared.IsSuccess.Should().Be(accepted);
        fixture.Engine.Matches.Should().Be(accepted ? 1 : 0);
        if (accepted)
            await AssertCannotReuseAsync(fixture, prepared, request, session);
    }

    private static FingerprintReferenceSession CreateSession(FingerprintReferenceTestData data) =>
        new(data.Authentication.GetEncoded(), data.Chuid, data.Fingerprint);

    private static FingerprintCeremonyService UsedService(
        FingerprintScenario fixture,
        string scenario
    ) =>
        scenario == "owner"
            ? new FingerprintCeremonyService(
                fixture.Authority,
                new FingerprintComparisonService(fixture.Engine),
                fixture.Clock
            )
            : fixture.Service;

    private static async Task AssertCannotReuseAsync(
        FingerprintScenario fixture,
        PreparedFingerprintReference prepared,
        CredentialVerificationRequest request,
        FingerprintReferenceSession session
    ) =>
        (
            await fixture.Service.ComparePreparedAsync(
                prepared,
                request,
                session,
                fixture.Capture,
                0,
                CancellationToken.None
            )
        )
            .IsFailure.Should()
            .BeTrue();

    private static CredentialVerificationRequest UsedRequest(
        CredentialVerificationRequest request,
        string scenario
    ) =>
        scenario == "request"
            ? new(
                request.EvaluatedAt,
                request.ClockUncertainty,
                request.ProfileId,
                request.Workflow,
                request.CredentialSnapshot,
                request.CredentialSha256,
                request.CardProof,
                request.HolderEvidence,
                request.Lifecycle,
                request.Bundle,
                false
            )
            : request;
}
