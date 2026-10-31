// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using System.Collections.Immutable;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using MatchingMinutia = OpenPhysical.Biometrics.NBIS.FingerprintMinutia;

[TestFixture]
internal sealed class FingerprintRecordAdapterTests
{
    private static FingerprintRecord Record(params FingerprintMinutia[] points) =>
        new(1, 1, 0x8001, 800, 768, 197, 197, [new(2, 0, 0, 60, points.ToImmutableArray(), [])]);

    [TestCase(0, 180)]
    [TestCase(1, 182)]
    [TestCase(89, 358)]
    [TestCase(90, 0)]
    [TestCase(179, 178)]
    public void TwoDegreeAnglesReverseDirectionAtTheirBoundaries(byte angle, int expected)
    {
        var record = Record(new FingerprintMinutia(1, 20, 168, angle, 50));
        var template = record.ToMatchingTemplate(FingerPosition.RightIndex, 0);
        Assert.That(
            template.Minutiae.Single(),
            Is.EqualTo(new MatchingMinutia(20, 600, expected, 50, 1))
        );
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void CoordinateEdgesQualityAndTypeArePreserved(byte type)
    {
        var record = Record(new FingerprintMinutia(type, 0, 0, 0, 0), new(type, 799, 767, 90, 100));
        var template = record.ToMatchingTemplate(FingerPosition.RightIndex, 0);
        Assert.That(
            template.Minutiae,
            Is.EqualTo(
                new[]
                {
                    new MatchingMinutia(0, 768, 180, 0, type),
                    new MatchingMinutia(799, 1, 0, 100, type),
                }
            )
        );
        Assert.That(record.Views[0].Minutiae[0].Y, Is.Zero);
    }

    [TestCase(196, 197)]
    [TestCase(197, 196)]
    [TestCase(500, 500)]
    [TestCase(0, 197)]
    public void ResolutionRequiresExplicitNominalFiveHundredPpi(int horizontal, int vertical)
    {
        var record = Record() with
        {
            XResolution = (ushort)horizontal,
            YResolution = (ushort)vertical,
        };
        Assert.Throws<NotSupportedException>(() =>
            record.ToMatchingTemplate(FingerPosition.RightIndex, 0)
        );
    }

    [Test]
    public void UnreportedQualityHasAnExplicitRankingLimit()
    {
        var points = Enumerable
            .Range(0, 151)
            .Select(index => new FingerprintMinutia(0, (ushort)index, 10, 0, 0))
            .ToArray();
        var accepted = Record(points[..150]).ToMatchingTemplate(FingerPosition.RightIndex, 0);
        Assert.That(accepted.Minutiae.Select(point => point.Quality), Is.All.Zero);
        Assert.Throws<NotSupportedException>(() =>
            Record(points).ToMatchingTemplate(FingerPosition.RightIndex, 0)
        );
        points[0] = points[0] with { Quality = 50 };
        Assert.Throws<NotSupportedException>(() =>
            Record(points).ToMatchingTemplate(FingerPosition.RightIndex, 0)
        );
        var ranked = Record(points.Select(point => point with { Quality = 50 }).ToArray())
            .ToMatchingTemplate(FingerPosition.RightIndex, 0);
        Assert.That(ranked.Minutiae, Has.Length.EqualTo(151));
    }

    [TestCase(101)]
    [TestCase(253)]
    public void InvalidViewQualityIsAnArgumentFailure(byte quality)
    {
        var record = Record(new FingerprintMinutia(1, 20, 168, 0, 75));
        record = record with { Views = [record.Views[0] with { Quality = quality }] };
        Assert.Throws<ArgumentException>(() =>
            record.ToMatchingTemplate(FingerPosition.RightIndex, 0)
        );
    }

    [TestCase(254)]
    [TestCase(255)]
    public void UnavailableViewIsSeparateFromInvalidQuality(byte quality)
    {
        var record = Record() with { Views = [new(2, 0, 0, quality, [], [])] };
        Assert.Throws<InvalidOperationException>(() =>
            record.ToMatchingTemplate(FingerPosition.RightIndex, 0)
        );
    }

    [Test]
    public void GeneralRecordsAllowUnreportedViewQualityAndEmptyTemplates()
    {
        var record = Record() with { Views = [new(2, 0, 0, 0, [], [])] };
        Assert.That(record.ToMatchingTemplate(FingerPosition.RightIndex, 0).Minutiae, Is.Empty);
    }

    [Test]
    public void CanonicalParsingRetainsEachExplicitFingerAndView()
    {
        var record = Record(new FingerprintMinutia(1, 20, 168, 0, 75));
        record = record with
        {
            Views = [record.Views[0], new(7, 1, 0, 60, [new(2, 30, 200, 179, 0)], [])],
        };
        var parsed = Incits378Codec.Parse(Incits378Codec.Write(record)).Value!;
        var selected = parsed.ToMatchingTemplate(FingerPosition.LeftIndex, 1);
        Assert.That(
            selected.Minutiae.Single(),
            Is.EqualTo(new MatchingMinutia(30, 568, 178, 0, 2))
        );
        Assert.Throws<ArgumentException>(() =>
            parsed.ToMatchingTemplate(FingerPosition.LeftIndex, 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            parsed.ToMatchingTemplate(FingerPosition.RightIndex, 16)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            parsed.ToMatchingTemplate((FingerPosition)0, 0)
        );
        var duplicated = parsed with { Views = [parsed.Views[1], parsed.Views[1]] };
        Assert.Throws<ArgumentException>(() =>
            duplicated.ToMatchingTemplate(FingerPosition.LeftIndex, 1)
        );
    }

    [Test]
    public void MalformedSelectedPointsFailBeforeEnteringTheNativeEngine()
    {
        FingerprintMinutia[] invalid =
        [
            new(3, 20, 10, 0, 50),
            new(1, 800, 10, 0, 50),
            new(1, 20, 768, 0, 50),
            new(1, 20, 10, 180, 50),
            new(1, 20, 10, 0, 101),
        ];
        foreach (var point in invalid)
            Assert.Throws<ArgumentException>(() =>
                Record(point).ToMatchingTemplate(FingerPosition.RightIndex, 0)
            );
        var oversized = Record(
            Enumerable
                .Range(0, 256)
                .Select(index => new FingerprintMinutia(1, (ushort)index, 10, 0, 50))
                .ToArray()
        );
        Assert.Throws<ArgumentException>(() =>
            oversized.ToMatchingTemplate(FingerPosition.RightIndex, 0)
        );
    }
}
