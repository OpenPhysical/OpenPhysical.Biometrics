// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using System.Collections.Immutable;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.NBIS;
using OpenPhysical.Biometrics.NFIQ1;
using MatchingMinutia = OpenPhysical.Biometrics.NBIS.FingerprintMinutia;

[TestFixture]
internal sealed class FingerprintEnrollmentTests
{
    [TestCase(1, 100)]
    [TestCase(2, 80)]
    [TestCase(3, 60)]
    [TestCase(4, 40)]
    [TestCase(5, 20)]
    public void QualityUsesOriginalNfiq1Mapping(int nfiq, int expected) =>
        Assert.That(
            new FingerprintQualityAssessment(nfiq, "test").PivQuality,
            Is.EqualTo(expected)
        );

    [Test]
    public void CaptureOwnsItsPixels()
    {
        var pixels = new byte[128 * 128];
        var capture = Capture(FingerPosition.RightIndex, pixels);
        pixels[0] = 255;
        Assert.That(capture.Image.Pixels[0], Is.Zero);
    }

    [TestCase(Nfiq1AssessmentStatus.EmptyImage, FingerprintQualityStatus.EmptyImage)]
    [TestCase(Nfiq1AssessmentStatus.TooFewMinutiae, FingerprintQualityStatus.InsufficientMinutiae)]
    public void QualityAdapterPreservesDefinedPoorImageOutcomes(
        Nfiq1AssessmentStatus status,
        FingerprintQualityStatus expected
    )
    {
        var provider = new Nfiq1FingerprintQualityProvider(new PoorImageEngine(status));
        var assessment = provider.Assess(Capture(FingerPosition.RightIndex));
        Assert.Multiple(() =>
        {
            Assert.That(assessment.Nfiq1, Is.EqualTo(5));
            Assert.That(assessment.PivQuality, Is.EqualTo(20));
            Assert.That(assessment.Confidence, Is.EqualTo(1));
            Assert.That(assessment.Status, Is.EqualTo(expected));
        });
    }

    [Test]
    public void IssuanceRequiresTheSuppliedEquipmentCertification()
    {
        var service = new PivFingerprintEnrollmentService(new TestEngine(), new TestQuality());
        var first = service.Prepare(Capture(FingerPosition.RightIndex));
        var second = service.Prepare(Capture(FingerPosition.LeftIndex));
        Assert.Throws<ArgumentException>(() =>
            service.CreateRecord(
                first,
                second,
                new(1, 2, 3, 128, 128, equipmentAppendixFCertified: false)
            )
        );
    }

    [Test]
    public void IssuanceSelectsHighestQualityAndPreservesSourceCoordinates()
    {
        var engine = new TestEngine();
        var service = new PivFingerprintEnrollmentService(engine, new TestQuality());
        var first = service.Prepare(Capture(FingerPosition.RightIndex));
        var second = service.Prepare(Capture(FingerPosition.LeftIndex));
        var record = service.CreateRecord(
            first,
            second,
            new(1, 2, 3, 128, 128, equipmentAppendixFCertified: true)
        );
        var encoded = Incits378Codec.Write(record);
        var decoded = Incits378Codec.Parse(encoded).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(decoded.Views, Has.Length.EqualTo(2));
            Assert.That(decoded.Views[0].Minutiae, Has.Length.EqualTo(128));
            Assert.That(decoded.Views[0].Minutiae[0].Quality, Is.EqualTo(100));
            Assert.That(decoded.Views[0].Minutiae[0].Y, Is.EqualTo(27));
            Assert.That(decoded.Views[0].Minutiae[0].Angle, Is.EqualTo(90));
            Assert.That(decoded.Views[0].Quality, Is.EqualTo(80));
            Assert.That(decoded.Views[0].ViewNumber, Is.Zero);
            Assert.That(decoded.CaptureEquipment, Is.EqualTo(0x8003));
            Assert.That(engine.Extractions, Is.EqualTo(2));
        });
    }

    [Test]
    public void AvailableSecondaryRequiresExplicitPrimarySelection()
    {
        var service = new PivFingerprintEnrollmentService(new TestEngine(), new TestQuality());
        var available = service.Prepare(Capture(FingerPosition.LeftIndex));
        var unavailable = FingerprintEnrollmentFinger.Unavailable(
            FingerPosition.RightIndex,
            FingerprintAvailability.TemporarilyUnusable
        );
        var metadata = new PivFingerprintRecordMetadata(
            1,
            2,
            3,
            128,
            128,
            equipmentAppendixFCertified: true
        );
        Assert.Throws<ArgumentException>(() =>
            service.CreateRecord(unavailable, available, metadata)
        );
        var ordered = service.CreateRecord(available, unavailable, metadata);
        Assert.That(
            ordered.Views.Select(view => view.FingerPosition),
            Is.EqualTo(new byte[] { 7, 2 })
        );
        Assert.That(ordered.Views.Select(view => view.Quality), Is.EqualTo(new byte[] { 80, 255 }));
    }

    [Test]
    public void UnavailableFingersCarryTheirDistinctProfileStates()
    {
        var service = new PivFingerprintEnrollmentService(new TestEngine(), new TestQuality());
        var record = service.CreateRecord(
            FingerprintEnrollmentFinger.Unavailable(
                FingerPosition.RightIndex,
                FingerprintAvailability.TemporarilyUnusable
            ),
            FingerprintEnrollmentFinger.Unavailable(
                FingerPosition.LeftIndex,
                FingerprintAvailability.PermanentlyUnusable
            ),
            new(1, 2, 3, 128, 128, equipmentAppendixFCertified: true)
        );
        Assert.Multiple(() =>
        {
            Assert.That(
                record.Views.Select(view => view.Quality),
                Is.EqualTo(new byte[] { 255, 254 })
            );
            Assert.That(record.Views.All(view => view.Minutiae.IsEmpty), Is.True);
            Assert.Throws<InvalidOperationException>(() =>
                FingerprintRecordAdapter.ToMatchingTemplate(record, FingerPosition.LeftIndex, 0)
            );
        });
    }

    [Test]
    public void CandidateRankingReusesTemplatesAndPrefersIndexFingersWithinQualityClass()
    {
        var engine = new TestEngine();
        var service = new PivFingerprintEnrollmentService(engine, new TestQuality());
        var candidates = new[]
        {
            FingerPosition.LeftThumb,
            FingerPosition.LeftIndex,
            FingerPosition.RightIndex,
        }
            .Select(finger => service.Prepare(Capture(finger)))
            .ToArray();
        var ranked = PivFingerprintEnrollmentService.RankCandidates(
            candidates,
            preferLeftHand: true
        );
        Assert.That(
            ranked.Select(candidate => candidate.Finger),
            Is.EqualTo(
                new[]
                {
                    FingerPosition.LeftIndex,
                    FingerPosition.RightIndex,
                    FingerPosition.LeftThumb,
                }
            )
        );
        Assert.That(engine.Extractions, Is.EqualTo(3));
    }

    [Test]
    public void ComparisonRecordsTheExplicitPolicyAndFingerSelection()
    {
        var engine = new TestEngine();
        var service = new PivFingerprintEnrollmentService(engine, new TestQuality());
        var probe = Capture(FingerPosition.RightIndex);
        var record = service.CreateRecord(
            service.Prepare(probe),
            service.Prepare(Capture(FingerPosition.LeftIndex)),
            new(1, 2, 3, 128, 128, equipmentAppendixFCertified: true)
        );
        var comparison = new FingerprintComparisonService(engine).Compare(
            probe,
            record,
            FingerPosition.RightIndex,
            0,
            new("site-policy-v3", 43)
        );
        Assert.That(
            comparison,
            Is.EqualTo(new FingerprintComparisonResult(42, false, "site-policy-v3"))
        );
        Assert.Throws<ArgumentException>(() =>
            new FingerprintComparisonService(engine).Compare(
                probe,
                record,
                FingerPosition.LeftIndex,
                0,
                new("site-policy-v3", 43)
            )
        );
    }

    [Test]
    public void AdapterRequiresExplicitResolutionAndView()
    {
        var record = new FingerprintRecord(
            1,
            2,
            0x8003,
            128,
            128,
            197,
            197,
            [new((byte)FingerPosition.RightIndex, 0, 0, 80, [new(1, 12, 23, 179, 70)], [])]
        );
        var template = FingerprintRecordAdapter.ToMatchingTemplate(
            record,
            FingerPosition.RightIndex,
            0
        );
        Assert.That(template.Minutiae[0], Is.EqualTo(new MatchingMinutia(12, 105, 178, 70, 1)));
        Assert.Throws<ArgumentException>(() =>
            FingerprintRecordAdapter.ToMatchingTemplate(record, FingerPosition.RightIndex, 1)
        );
        Assert.Throws<NotSupportedException>(() =>
            FingerprintRecordAdapter.ToMatchingTemplate(
                record with
                {
                    XResolution = 196,
                },
                FingerPosition.RightIndex,
                0
            )
        );
    }

    private static FingerprintCapture Capture(FingerPosition finger, byte[]? pixels = null) =>
        new(
            pixels ?? new byte[128 * 128],
            128,
            128,
            500,
            finger,
            DateTimeOffset.UnixEpoch,
            FingerprintImpression.PlainLiveScan,
            3,
            equipmentAppendixFCertified: true
        );

    private sealed class TestQuality : IFingerprintQualityProvider
    {
        public FingerprintQualityAssessment Assess(FingerprintCapture capture) =>
            new(2, "test-provider");
    }

    private sealed class PoorImageEngine(Nfiq1AssessmentStatus status) : INfiq1Engine
    {
        public Nfiq1Assessment Assess(
            ReadOnlySpan<byte> pixels,
            int width,
            int height,
            int dotsPerInch
        ) => new(5, 1, status);
    }

    private sealed class TestEngine : IFingerprintEngine
    {
        internal int Extractions { get; private set; }

        public FingerprintTemplate Extract(
            ReadOnlySpan<byte> pixels,
            int width,
            int height,
            int dotsPerInch
        )
        {
            Extractions++;
            return new(
                width,
                height,
                Enumerable
                    .Range(0, 200)
                    .Select(index => new MatchingMinutia(
                        index % 128,
                        1 + index % 128,
                        0,
                        index % 101,
                        1
                    )),
                "test-engine"
            );
        }

        public int Match(FingerprintTemplate probe, FingerprintTemplate gallery) => 42;
    }
}
