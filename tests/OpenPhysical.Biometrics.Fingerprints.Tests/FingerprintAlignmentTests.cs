// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;

[TestFixture]
internal sealed class FingerprintAlignmentTests
{
    [TestCase(0.0, 1.0)]
    [TestCase(31.0, 1.08)]
    [TestCase(-62.0, 0.91)]
    [TestCase(147.0, 1.0)]
    public void RecoversSimilarityDespiteMissingPointsOutliersAndQuantization(
        double degrees,
        double scale
    )
    {
        var reference = AlignmentTestData.Record(AlignmentTestData.Points());
        var probe = AlignmentTestData.Transform(reference, degrees, scale, 900, 900, 197, 197);
        probe = probe with
        {
            Views =
            [
                probe.Views[0] with
                {
                    Minutiae = probe
                        .Views[0]
                        .Minutiae.Skip(9)
                        .Concat(AlignmentTestData.Points(99).Take(15))
                        .ToImmutableArray(),
                },
            ],
        };
        var original = Incits378Codec.Write(reference).Value;
        var result = FingerprintAlignment.Align(reference, 0, probe, 0);
        result.Should().Succeed();
        result.Value.Pairs.Should().HaveCountGreaterThanOrEqualTo(30);
        result.Value.RootMeanSquareError.Should().BeLessThan(1.1);
        result.Value.Transform.Scale.Should().BeApproximately(scale, 0.005);
        result.Value.ReferenceMinutiae.Should().HaveCount(reference.Views[0].Minutiae.Length);
        Incits378Codec.Write(reference).Value.Should().Equal(original);
    }

    [Test]
    public void ResolutionIsNormalizedBeforeAlignmentAndProjectedBackIntoProbePixels()
    {
        var reference = AlignmentTestData.Record(AlignmentTestData.Points());
        var probe = AlignmentTestData.Transform(reference, 22, 1.03, 120, 80, 394, 295);
        var result = FingerprintAlignment.Align(reference, 0, probe, 0).Value;
        result.Pairs.Should().HaveCount(reference.Views[0].Minutiae.Length);
        result.Transform.Scale.Should().BeApproximately(1.03, 0.004);
        foreach (var pair in result.Pairs)
        {
            var displayed = result.ReferenceMinutiae[pair.ReferenceIndex];
            var measured = probe.Views[0].Minutiae[pair.ProbeIndex];
            displayed.X.Should().BeApproximately(measured.X, 1.5);
            displayed.Y.Should().BeApproximately(measured.Y, 1.5);
            displayed.AngleDegrees.Should().BeApproximately(measured.Angle * 2, 2.5);
        }
    }

    [Test]
    public void UnrelatedConstellationsDoNotProduceAnOverlay() =>
        FingerprintAlignment
            .Align(
                AlignmentTestData.Record(AlignmentTestData.Points(3)),
                0,
                AlignmentTestData.Record(AlignmentTestData.Points(9)),
                0
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.AlignmentNotEstablished);

    [Test]
    public void ReusedProbePointsCannotInflateSupport()
    {
        var reference = AlignmentTestData.Record(AlignmentTestData.Points());
        var one = reference.Views[0].Minutiae[0];
        var duplicates = AlignmentTestData.Record(Enumerable.Repeat(one, 40).ToImmutableArray());
        FingerprintAlignment
            .Align(reference, 0, duplicates, 0)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.AlignmentNotEstablished);
    }

    [Test]
    public void CrossExtractorTypeDisagreementsDoNotDiscardGeometricCorrespondences()
    {
        var reference = AlignmentTestData.Record(AlignmentTestData.Points());
        var probe = AlignmentTestData.Transform(reference, 24, 1.04, 400, 200, 197, 197);
        probe = probe with
        {
            Views =
            [
                probe.Views[0] with
                {
                    Minutiae = probe
                        .Views[0]
                        .Minutiae.Select(point => point with { Type = (byte)(3 - point.Type) })
                        .ToImmutableArray(),
                },
            ],
        };
        var result = FingerprintAlignment.Align(reference, 0, probe, 0);
        result.Should().Succeed();
        var aligned = result.Value;
        aligned.Pairs.Should().HaveCount(reference.Views[0].Minutiae.Length);
        aligned.RootMeanSquareError.Should().BeLessThan(1);
    }

    [Test]
    public void TwoEquallySupportedDistinctPlacementsAreRejected()
    {
        var reference = AlignmentTestData.Record(AlignmentTestData.Points());
        var shifted = reference
            .Views[0]
            .Minutiae.Select(point => point with { X = (ushort)(point.X + 800) });
        var probe = AlignmentTestData.Record(
            reference.Views[0].Minutiae.Concat(shifted).ToImmutableArray()
        );
        FingerprintAlignment
            .Align(reference, 0, probe, 0)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.AlignmentNotEstablished);
    }

    [Test]
    public void NullExternalRecordsAndViewsFailBeforePointProcessing()
    {
        var record = AlignmentTestData.Record(AlignmentTestData.Points());
        FingerprintAlignment
            .Align(null!, 0, record, 0)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.InvalidAlignmentInput);
        FingerprintAlignment
            .Align(record with { Views = [null!] }, 0, record, 0)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.InvalidAlignmentInput);
        var points = record.Views[0].Minutiae.SetItem(0, null!);
        FingerprintAlignment
            .Align(
                record with
                {
                    Views = [record.Views[0] with { Minutiae = points }],
                },
                0,
                record,
                0
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.InvalidAlignmentInput);
    }

    [Test]
    public void InvalidSelectionResolutionAndInsufficientPointsAreTypedFailures()
    {
        var record = AlignmentTestData.Record(AlignmentTestData.Points());
        FingerprintAlignment
            .Align(record, -1, record, 0)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.ViewSelection);
        FingerprintAlignment
            .Align(record with { XResolution = 0 }, 0, record, 0)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.InvalidAlignmentInput);
        var sparse = record with
        {
            Views = [record.Views[0] with { Minutiae = [record.Views[0].Minutiae[0]] }],
        };
        FingerprintAlignment
            .Align(sparse, 0, record, 0)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.InvalidAlignmentInput);
    }
}
