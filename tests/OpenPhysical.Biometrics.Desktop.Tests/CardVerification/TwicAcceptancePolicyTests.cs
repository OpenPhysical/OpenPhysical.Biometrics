// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using System.Text.Json;
using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure.Twic;

namespace OpenPhysical.Middleware.Tests;

[TestFixture]
internal sealed class TwicAcceptancePolicyTests
{
    [TestCase(TwicCardModel.Legacy, false, false, false, "direct")]
    [TestCase(TwicCardModel.Nexgen, false, false, false, "direct")]
    [TestCase(TwicCardModel.Nexgen, true, false, false, "direct")]
    [TestCase(TwicCardModel.Nexgen, false, true, false, "direct")]
    [TestCase(TwicCardModel.Legacy, false, false, true, "direct")]
    [TestCase(TwicCardModel.Nexgen, true, false, true, "direct")]
    [TestCase(TwicCardModel.Legacy, false, false, false, "prepared")]
    [TestCase(TwicCardModel.Nexgen, false, false, false, "prepared")]
    [TestCase(TwicCardModel.Nexgen, false, false, false, "wrong-key")]
    [TestCase(TwicCardModel.Nexgen, false, false, false, "changed-object")]
    [TestCase(TwicCardModel.Nexgen, false, false, false, "changed-generation")]
    [TestCase(TwicCardModel.Nexgen, false, false, false, "reset")]
    [TestCase(TwicCardModel.Nexgen, false, false, false, "disposed")]
    [TestCase(TwicCardModel.Legacy, false, false, false, "live")]
    [TestCase(TwicCardModel.Nexgen, false, false, false, "live")]
    [TestCase(TwicCardModel.Legacy, false, false, false, "live-no-card-proof")]
    [TestCase(TwicCardModel.Nexgen, false, false, false, "live-contact-key")]
    [TestCase(TwicCardModel.Nexgen, false, false, false, "live-contactless-key-missing")]
    [TestCase(TwicCardModel.Nexgen, false, false, false, "live-reset")]
    [TestCase(TwicCardModel.Nexgen, false, false, false, "live-cancel")]
    [TestCase(TwicCardModel.Nexgen, false, false, false, "live-wrong-key")]
    [TestCase(TwicCardModel.Nexgen, false, true, false, "live")]
    public Task TwicRequiresExecutedDecryptionAndMatchPlusIndependentCcl(
        TwicCardModel generation,
        bool forged,
        bool cancelled,
        bool attended,
        string path
    ) => TwicAcceptanceAssertions.RunAsync(new(generation, forged, cancelled, attended, path));

    [Test]
    public void SignedPolicyBindsSubsecondAgeWithoutTruncation()
    {
        var first = TwicAcceptanceService.EncodeProfile(Profile(TimeSpan.FromSeconds(1)));
        var second = TwicAcceptanceService.EncodeProfile(
            Profile(TimeSpan.FromSeconds(1).Add(TimeSpan.FromTicks(1)))
        );
        second.Should().NotEqual(first);
        using var json = JsonDocument.Parse(second.ToArray());
        json.RootElement.GetProperty("cancellationMaximumAgeTicks")
            .GetProperty("1")
            .GetString()
            .Should()
            .Be("10000001");
    }

    private static TwicAcceptanceProfile Profile(TimeSpan age) =>
        new(
            "twic.fixture.v1",
            1,
            TwicCardModel.Nexgen,
            TwicAcceptanceWorkflow.DirectInspection,
            "fixture",
            true,
            true,
            false,
            ImmutableDictionary<int, TimeSpan>.Empty.Add(1, age).Add(2, age).Add(3, age)
        );
}
