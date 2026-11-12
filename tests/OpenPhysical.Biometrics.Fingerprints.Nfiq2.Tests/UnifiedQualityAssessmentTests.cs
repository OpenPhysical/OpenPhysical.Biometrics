// SPDX-License-Identifier: GPL-2.0-or-later
using AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Fingerprints.Nfiq2.Tests;

[TestFixture]
internal sealed class UnifiedQualityAssessmentTests
{
    [TestCase(0)]
    [TestCase(54)]
    [TestCase(100)]
    public void ContinuousScorePreservesBothEndpointsAndIdentity(int score)
    {
        var result = FingerprintUnifiedQualityAssessment.Create(score, "NFIQ 2.3.0");
        result.IsSuccess.Should().BeTrue();
        result.Value.Score.Should().Be(score);
        result.Value.AlgorithmVersion.Should().Be("NFIQ 2.3.0");
    }

    [TestCase(-1, "NFIQ 2.3.0")]
    [TestCase(101, "NFIQ 2.3.0")]
    [TestCase(54, "")]
    [TestCase(54, " ")]
    public void InvalidScoresOrMissingIdentityFailPrecisely(int score, string identity)
    {
        var result = FingerprintUnifiedQualityAssessment.Create(score, identity);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(FingerprintErrorCodes.InvalidQuality);
    }
}
