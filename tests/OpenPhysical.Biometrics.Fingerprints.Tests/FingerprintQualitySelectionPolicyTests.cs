// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class FingerprintQualitySelectionPolicyTests
{
    [Test]
    public void DefaultsUseOneClassOrFivePointApplicationHysteresis()
    {
        FingerprintQualitySelectionPolicy.Default.MinimumNfiq1Gain.Should().Be(1);
        FingerprintQualitySelectionPolicy.Default.MinimumNfiq2Gain.Should().Be(5);
        var adjusted = FingerprintQualitySelectionPolicy.Create(2, 7).Value;
        adjusted.MinimumNfiq1Gain.Should().Be(2);
        adjusted.MinimumNfiq2Gain.Should().Be(7);
    }

    [TestCase(0, 5)]
    [TestCase(5, 5)]
    [TestCase(1, 0)]
    [TestCase(1, 101)]
    public void GainsMustRequireARealImprovementWithinEachScoreScale(int nfiq1, int nfiq2)
    {
        var result = FingerprintQualitySelectionPolicy.Create(nfiq1, nfiq2);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(FingerprintErrorCodes.InvalidSelectionPolicy);
    }
}
