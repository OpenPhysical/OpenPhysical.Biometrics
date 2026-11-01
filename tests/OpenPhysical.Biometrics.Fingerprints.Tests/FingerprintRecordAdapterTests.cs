// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
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
        template.Minutiae.Single().Should().Be(new MatchingMinutia(20, 600, expected, 50, 1));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void CoordinateEdgesQualityAndTypeArePreserved(byte type)
    {
        var record = Record(new FingerprintMinutia(type, 0, 0, 0, 0), new(type, 799, 767, 90, 100));
        var template = record.ToMatchingTemplate(FingerPosition.RightIndex, 0);
        template
            .Minutiae.Should()
            .Equal(
                new[]
                {
                    new MatchingMinutia(0, 768, 180, 0, type),
                    new MatchingMinutia(799, 1, 0, 100, type),
                }
            );
        record.Views[0].Minutiae[0].Y.Should().Be(0);
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
        FluentActions
            .Invoking(() => record.ToMatchingTemplate(FingerPosition.RightIndex, 0))
            .Should()
            .ThrowExactly<NotSupportedException>();
    }

    [Test]
    public void UnreportedQualityHasAnExplicitRankingLimit()
    {
        var points = Enumerable
            .Range(0, 151)
            .Select(index => new FingerprintMinutia(0, (ushort)index, 10, 0, 0))
            .ToArray();
        var accepted = Record(points[..150]).ToMatchingTemplate(FingerPosition.RightIndex, 0);
        accepted.Minutiae.Select(point => point.Quality).Should().OnlyContain(x => x == 0);
        FluentActions
            .Invoking(() => Record(points).ToMatchingTemplate(FingerPosition.RightIndex, 0))
            .Should()
            .ThrowExactly<NotSupportedException>();
        points[0] = points[0] with { Quality = 50 };
        FluentActions
            .Invoking(() => Record(points).ToMatchingTemplate(FingerPosition.RightIndex, 0))
            .Should()
            .ThrowExactly<NotSupportedException>();
        var ranked = Record(points.Select(point => point with { Quality = 50 }).ToArray())
            .ToMatchingTemplate(FingerPosition.RightIndex, 0);
        ranked.Minutiae.Should().HaveCount(151);
    }

    [TestCase(101)]
    [TestCase(253)]
    public void InvalidViewQualityIsAnArgumentFailure(byte quality)
    {
        var record = Record(new FingerprintMinutia(1, 20, 168, 0, 75));
        record = record with { Views = [record.Views[0] with { Quality = quality }] };
        FluentActions
            .Invoking(() => record.ToMatchingTemplate(FingerPosition.RightIndex, 0))
            .Should()
            .ThrowExactly<ArgumentException>();
    }

    [TestCase(254)]
    [TestCase(255)]
    public void UnavailableViewIsSeparateFromInvalidQuality(byte quality)
    {
        var record = Record() with { Views = [new(2, 0, 0, quality, [], [])] };
        FluentActions
            .Invoking(() => record.ToMatchingTemplate(FingerPosition.RightIndex, 0))
            .Should()
            .ThrowExactly<InvalidOperationException>();
    }

    [Test]
    public void GeneralRecordsAllowUnreportedViewQualityAndEmptyTemplates()
    {
        var record = Record() with { Views = [new(2, 0, 0, 0, [], [])] };
        record.ToMatchingTemplate(FingerPosition.RightIndex, 0).Minutiae.Should().BeEmpty();
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
        selected.Minutiae.Single().Should().Be(new MatchingMinutia(30, 568, 178, 0, 2));
        FluentActions
            .Invoking(() => parsed.ToMatchingTemplate(FingerPosition.LeftIndex, 0))
            .Should()
            .ThrowExactly<ArgumentException>();
        FluentActions
            .Invoking(() => parsed.ToMatchingTemplate(FingerPosition.RightIndex, 16))
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();
        FluentActions
            .Invoking(() => parsed.ToMatchingTemplate((FingerPosition)0, 0))
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();
        var duplicated = parsed with { Views = [parsed.Views[1], parsed.Views[1]] };
        FluentActions
            .Invoking(() => duplicated.ToMatchingTemplate(FingerPosition.LeftIndex, 1))
            .Should()
            .ThrowExactly<ArgumentException>();
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
            FluentActions
                .Invoking(() => Record(point).ToMatchingTemplate(FingerPosition.RightIndex, 0))
                .Should()
                .ThrowExactly<ArgumentException>();
        var oversized = Record(
            Enumerable
                .Range(0, 256)
                .Select(index => new FingerprintMinutia(1, (ushort)index, 10, 0, 50))
                .ToArray()
        );
        FluentActions
            .Invoking(() => oversized.ToMatchingTemplate(FingerPosition.RightIndex, 0))
            .Should()
            .ThrowExactly<ArgumentException>();
    }
}
