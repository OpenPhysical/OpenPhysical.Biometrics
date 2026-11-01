// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
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
        new FingerprintQualityAssessment(nfiq, "test").PivQuality.Should().Be((byte)expected);

    [Test]
    public void CaptureOwnsItsPixels()
    {
        var pixels = new byte[128 * 128];
        var capture = Capture(FingerPosition.RightIndex, pixels);
        pixels[0] = 255;
        capture.Image.Pixels[0].Should().Be(0);
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
        using (new AssertionScope())
        {
            assessment.Nfiq1.Should().Be(5);
            assessment.PivQuality.Should().Be(20);
            assessment.Confidence.Should().Be(1);
            assessment.Status.Should().Be(expected);
        }
    }

    [Test]
    public void IssuanceRequiresTheSuppliedEquipmentCertification()
    {
        var service = new PivFingerprintEnrollmentService(new TestEngine(), new TestQuality());
        var first = service.Prepare(Capture(FingerPosition.RightIndex));
        var second = service.Prepare(Capture(FingerPosition.LeftIndex));
        FluentActions
            .Invoking(() =>
                service.CreateRecord(
                    first,
                    second,
                    new(1, 2, 3, 128, 128, equipmentAppendixFCertified: false)
                )
            )
            .Should()
            .ThrowExactly<ArgumentException>();
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
        using (new AssertionScope())
        {
            decoded.Views.Should().HaveCount(2);
            decoded.Views[0].Minutiae.Should().HaveCount(128);
            decoded.Views[0].Minutiae[0].Quality.Should().Be(100);
            decoded.Views[0].Minutiae[0].Y.Should().Be(27);
            decoded.Views[0].Minutiae[0].Angle.Should().Be(90);
            decoded.Views[0].Quality.Should().Be(80);
            decoded.Views[0].ViewNumber.Should().Be(0);
            decoded.CaptureEquipment.Should().Be(0x8003);
            engine.Extractions.Should().Be(2);
        }
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
        FluentActions
            .Invoking(() => service.CreateRecord(unavailable, available, metadata))
            .Should()
            .ThrowExactly<ArgumentException>();
        var ordered = service.CreateRecord(available, unavailable, metadata);
        ordered.Views.Select(view => view.FingerPosition).Should().Equal(new byte[] { 7, 2 });
        ordered.Views.Select(view => view.Quality).Should().Equal(new byte[] { 80, 255 });
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
        using (new AssertionScope())
        {
            record.Views.Select(view => view.Quality).Should().Equal(new byte[] { 255, 254 });
            record.Views.All(view => view.Minutiae.IsEmpty).Should().BeTrue();
            FluentActions
                .Invoking(() =>
                    FingerprintRecordAdapter.ToMatchingTemplate(record, FingerPosition.LeftIndex, 0)
                )
                .Should()
                .ThrowExactly<InvalidOperationException>();
        }
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
        ranked
            .Select(candidate => candidate.Finger)
            .Should()
            .Equal(
                new[]
                {
                    FingerPosition.LeftIndex,
                    FingerPosition.RightIndex,
                    FingerPosition.LeftThumb,
                }
            );
        engine.Extractions.Should().Be(3);
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
        comparison.Should().Be(new FingerprintComparisonResult(42, false, "site-policy-v3"));
        FluentActions
            .Invoking(() =>
                new FingerprintComparisonService(engine).Compare(
                    probe,
                    record,
                    FingerPosition.LeftIndex,
                    0,
                    new("site-policy-v3", 43)
                )
            )
            .Should()
            .ThrowExactly<ArgumentException>();
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
        template.Minutiae[0].Should().Be(new MatchingMinutia(12, 105, 178, 70, 1));
        FluentActions
            .Invoking(() =>
                FingerprintRecordAdapter.ToMatchingTemplate(record, FingerPosition.RightIndex, 1)
            )
            .Should()
            .ThrowExactly<ArgumentException>();
        FluentActions
            .Invoking(() =>
                FingerprintRecordAdapter.ToMatchingTemplate(
                    record with
                    {
                        XResolution = 196,
                    },
                    FingerPosition.RightIndex,
                    0
                )
            )
            .Should()
            .ThrowExactly<NotSupportedException>();
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
