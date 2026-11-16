// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class FingerprintAssessmentTests
{
    [Test]
    public void AssessingSeveralCandidatesDoesNotExtractAndOnlyTheWinnerIsExtracted()
    {
        var engine = new TestEngine();
        var quality = new TestQuality();
        var service = new FingerprintPreparationService(engine, quality);
        var first = TestCaptures.Capture(FingerPosition.RightIndex).Image;
        var winner = TestCaptures.Capture(FingerPosition.LeftIndex).Image;
        service.Assess(first).IsSuccess.Should().BeTrue();
        var assessed = service.Assess(winner).Value;
        quality.Assessments.Should().Be(2);
        engine.Extractions.Should().Be(0);
        var prepared = service.Extract(assessed).Value;
        quality.Assessments.Should().Be(2);
        engine.Extractions.Should().Be(1);
        assessed.Image.Should().BeSameAs(winner);
        prepared.Image.Should().BeSameAs(assessed.Image);
        prepared.Quality.Should().BeSameAs(assessed.Quality);
    }

    [Test]
    public void InvalidAssessmentCannotBecomeAnExtractionCandidate()
    {
        var engine = new TestEngine();
        var quality = new TestQuality(0);
        var service = new FingerprintPreparationService(engine, quality);
        var assessed = service.Assess(TestCaptures.Capture(FingerPosition.RightIndex).Image);
        assessed.IsFailure.Should().BeTrue();
        assessed.Error.Code.Should().Be(FingerprintErrorCodes.InvalidQuality);
        quality.Assessments.Should().Be(1);
        engine.Extractions.Should().Be(0);
    }
}
