// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class FingerprintPreparationTests
{
    [Test]
    public void RejectedImageQualityCannotReachExtraction()
    {
        var engine = new TestEngine();
        var quality = new TestQuality(nfiq: 0);
        new FingerprintPreparationService(engine, quality)
            .Prepare(TestCaptures.Capture(FingerPosition.RightIndex).Image)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.InvalidQuality);
        quality.Assessments.Should().Be(1);
        engine.Extractions.Should().Be(0);
    }

    [Test]
    public void ImagePreparationRetainsTheExactImageAndRunsEachProviderOnce()
    {
        var engine = new TestEngine();
        var quality = new TestQuality();
        var image = TestCaptures.Capture(FingerPosition.RightIndex).Image;
        var prepared = new FingerprintPreparationService(engine, quality).Prepare(image).Value;
        prepared.Image.Should().BeSameAs(image);
        prepared.Template.Provenance.Should().Be("test-engine");
        prepared.Quality.Nfiq1.Should().Be(2);
        engine.Extractions.Should().Be(1);
        quality.Assessments.Should().Be(1);
    }

    [Test]
    public void PivEnrollmentDelegatesToTheSameReusableImagePreparation()
    {
        var engine = new TestEngine();
        var quality = new TestQuality();
        var processing = new FingerprintPreparationService(engine, quality);
        var capture = TestCaptures.Capture(FingerPosition.RightIndex);
        var prepared = new PivFingerprintEnrollmentService(processing).Prepare(capture).Value;
        prepared.Capture.Value.Should().BeSameAs(capture);
        prepared.Template.Value.Provenance.Should().Be("test-engine");
        prepared.Quality.Value.Nfiq1.Should().Be(2);
        engine.Extractions.Should().Be(1);
        quality.Assessments.Should().Be(1);
    }
}
