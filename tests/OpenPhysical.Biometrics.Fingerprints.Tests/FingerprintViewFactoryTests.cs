// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.NBIS;
using MatchingMinutia = OpenPhysical.Biometrics.NBIS.FingerprintMinutia;

[TestFixture]
internal sealed class FingerprintViewFactoryTests
{
    [Test]
    public void SingleFingerUsesTheSameCanonicalPointsAsPivIssuance()
    {
        var processing = new FingerprintPreparationService(new TestEngine(), new TestQuality());
        var capture = TestCaptures.Capture(FingerPosition.RightIndex);
        var prepared = processing.Prepare(capture.Image).Value;
        var view = FingerprintViewFactory
            .Create(prepared.Template, prepared.Quality, capture.Finger, 0, capture.Impression)
            .Value;
        var enrollment = new PivFingerprintEnrollmentService(processing);
        var piv = enrollment
            .CreateRecord(
                enrollment.Prepare(capture).Value,
                enrollment.Prepare(TestCaptures.Capture(FingerPosition.LeftIndex)).Value,
                TestCaptures.Metadata()
            )
            .Value;
        view.Minutiae.Should().Equal(piv.Views[0].Minutiae);
        view.Minutiae.Should().HaveCount(128);
        view.Quality.Should().Be(80);
        var record = new FingerprintRecord(0, 0, 0, 128, 128, 197, 197, [view]);
        var bytes = Incits378Codec.Write(record).Value;
        var reloaded = Incits378Codec.Parse(bytes).Value.Record;
        reloaded.Views[0].Minutiae.Should().Equal(view.Minutiae);
    }

    [Test]
    public void MoreThan128DistinctPointsRetainTheDeterministicHighestQualitySubset()
    {
        var source = new FingerprintTemplate(
            256,
            128,
            Enumerable.Range(0, 200).Select(index => new MatchingMinutia(index, 100, 1, 50, 1)),
            "test"
        );
        var view = FingerprintViewFactory
            .Create(
                source,
                FingerprintQualityAssessment.Create(1, "test").Value,
                FingerPosition.RightIndex,
                0,
                FingerprintImpression.PlainLiveScan
            )
            .Value;
        view.Minutiae.Select(point => point.X)
            .Should()
            .Equal(Enumerable.Range(0, 128).Select(x => (ushort)x));
        source.Minutiae.Should().HaveCount(200);
    }

    [Test]
    public void ExportedPointsRetainQuantizedAnglesAndCanonicalComparisonCoordinates()
    {
        var source = new FingerprintTemplate(
            128,
            128,
            [new MatchingMinutia(20, 100, 1, 90, 1)],
            "test"
        );
        var view = FingerprintViewFactory
            .Create(
                source,
                FingerprintQualityAssessment.Create(1, "test").Value,
                FingerPosition.LeftIndex,
                0,
                FingerprintImpression.PlainLiveScan
            )
            .Value;
        view.Minutiae.Single()
            .Should()
            .Be(new Formats.Fingerprints.FingerprintMinutia(1, 20, 28, 90, 90));
        var record = new FingerprintRecord(0, 0, 0, 128, 128, 197, 197, [view]);
        var matching = record.ToMatchingTemplate(FingerPosition.LeftIndex, 0).Value;
        matching.Minutiae.Single().Should().Be(new MatchingMinutia(20, 100, 0, 90, 1));
    }

    [TestCase(0, 0, 0, FingerprintErrorCodes.InvalidFinger)]
    [TestCase(2, 16, 0, FingerprintErrorCodes.InvalidViewNumber)]
    [TestCase(2, 0, 1, FingerprintErrorCodes.InvalidImpression)]
    public void InvalidViewMetadataFailsBeforeEncoding(
        int finger,
        byte view,
        int impression,
        string code
    )
    {
        var prepared = new FingerprintPreparationService(new TestEngine(), new TestQuality())
            .Prepare(TestCaptures.Capture(FingerPosition.RightIndex).Image)
            .Value;
        FingerprintViewFactory
            .Create(
                prepared.Template,
                prepared.Quality,
                (FingerPosition)finger,
                view,
                (FingerprintImpression)impression
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(code);
    }
}
