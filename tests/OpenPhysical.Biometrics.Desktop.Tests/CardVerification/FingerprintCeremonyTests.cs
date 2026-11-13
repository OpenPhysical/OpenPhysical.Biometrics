// SPDX-License-Identifier: GPL-2.0-or-later
using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Middleware.Infrastructure.Trust;

namespace OpenPhysical.Middleware.Tests;

[TestFixture]
internal sealed class FingerprintCeremonyTests
{
    [TestCase(false, 100, true)]
    [TestCase(false, 10, false)]
    [TestCase(true, 100, false)]
    [TestCase(false, 100, true, "prepared")]
    [TestCase(true, 100, false, "prepared")]
    [TestCase(false, 100, false, "reset")]
    [TestCase(false, 100, false, "session")]
    [TestCase(false, 100, false, "request")]
    [TestCase(false, 100, false, "owner")]
    [TestCase(false, 100, false, "expiry")]
    [TestCase(false, 100, false, "capture-before")]
    [TestCase(false, 100, true, "acquisition")]
    [TestCase(false, 100, true, "acquisition-pin")]
    [TestCase(false, 100, false, "acquisition-reset")]
    [TestCase(false, 100, false, "acquisition-unqualified")]
    [TestCase(false, 100, false, "acquisition-policy-missing")]
    [TestCase(false, 100, false, "acquisition-cancel")]
    [TestCase(false, 100, false, "acquisition-wrong-key")]
    public async Task OnlyAuthenticatedReferenceReachesComparison_AndThresholdControlsReceipt(
        bool corruptReference,
        int score,
        bool accepted,
        string scenario = "legacy"
    )
    {
        var fixture = await FingerprintScenario.CreateAsync(corruptReference, score, scenario);
        await fixture.AssertAsync(corruptReference, accepted, scenario);
    }

    [Test]
    public void PreparedReferencesHaveNoPublicConstructionPath() =>
        typeof(PreparedFingerprintReference).GetConstructors().Should().BeEmpty();
}
