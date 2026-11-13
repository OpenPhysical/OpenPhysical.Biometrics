// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure.Trust;

namespace OpenPhysical.Middleware.Tests;

internal static class TwicPreparedAssertions
{
    internal static async Task<Maybe<HolderVerificationEvidence>> RunAsync(TwicScenario fixture)
    {
        var key = new TwicPrivacyKeyContainer(
            fixture.Reference.Key,
            TwicPrivacyKeyContainer.AlgorithmAesEcb,
            0
        );
        if (fixture.Options.Path == "direct")
        {
            var result = await fixture.Fingerprints.CompareTwicAsync(
                fixture.Bare,
                key,
                fixture.Capture,
                0,
                CancellationToken.None
            );
            result
                .IsSuccess.Should()
                .BeTrue(result.IsFailure ? result.Error.Operation : string.Empty);
            return result.Value;
        }
        return await PreparedAsync(fixture, key);
    }

    private static async Task<Maybe<HolderVerificationEvidence>> PreparedAsync(
        TwicScenario fixture,
        TwicPrivacyKeyContainer key
    )
    {
        var path = fixture.Options.Path;
        using var session = new TwicReferenceSession(
            path == "changed-generation" ? TwicCardModel.Legacy : fixture.Options.Generation,
            fixture.Reference.Chuid,
            path == "changed-object" ? ImmutableArray.Create((byte)0) : fixture.Reference.Encrypted
        );
        if (path == "wrong-key")
            key = new(new byte[16].ToImmutableArray(), TwicPrivacyKeyContainer.AlgorithmAesEcb, 0);
        var prepared = await fixture.Fingerprints.PrepareTwicAsync(
            fixture.Bare,
            fixture.Policy.Profile,
            session,
            key,
            CancellationToken.None
        );
        if (path is "wrong-key" or "changed-object" or "changed-generation")
        {
            prepared.IsFailure.Should().BeTrue();
            fixture.Engine.Matches.Should().Be(0);
            return Maybe<HolderVerificationEvidence>.None;
        }
        prepared
            .IsSuccess.Should()
            .BeTrue(prepared.IsFailure ? prepared.Error.Operation : string.Empty);
        using var owned = prepared.Value;
        owned.Selections.Should().Equal(new[] { new FingerprintReferenceSelection(2, 0) });
        if (path == "reset")
            session.Current = false;
        if (path == "disposed")
            owned.Dispose();
        return await CompareAsync(fixture, session, owned);
    }

    private static async Task<Maybe<HolderVerificationEvidence>> CompareAsync(
        TwicScenario fixture,
        TwicReferenceSession session,
        PreparedFingerprintReference owned
    )
    {
        var executed = await fixture.Fingerprints.ComparePreparedAsync(
            owned,
            fixture.Bare,
            session,
            fixture.Capture,
            0,
            CancellationToken.None
        );
        if (fixture.Options.Path is "reset" or "disposed")
        {
            executed.IsFailure.Should().BeTrue();
            fixture.Engine.Matches.Should().Be(0);
            return Maybe<HolderVerificationEvidence>.None;
        }
        await AssertCannotReuseAsync(fixture, session, owned);
        session
            .ProofCommands.Should()
            .Be(
                0,
                "reference preparation must not invent a mandatory card-authentication ceremony"
            );
        executed
            .IsSuccess.Should()
            .BeTrue(executed.IsFailure ? executed.Error.Operation : string.Empty);
        return executed.Value;
    }

    private static async Task AssertCannotReuseAsync(
        TwicScenario fixture,
        TwicReferenceSession session,
        PreparedFingerprintReference owned
    ) =>
        (
            await fixture.Fingerprints.ComparePreparedAsync(
                owned,
                fixture.Bare,
                session,
                fixture.Capture,
                0,
                CancellationToken.None
            )
        )
            .IsFailure.Should()
            .BeTrue();
}
