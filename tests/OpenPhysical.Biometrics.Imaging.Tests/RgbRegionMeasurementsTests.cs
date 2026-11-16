// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Imaging.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

internal sealed class RgbRegionMeasurementsTests
{
    [Test]
    public void PixelStatisticsUseTheSelectedNativeSamplesWithoutInventingEmptyEvidence()
    {
        byte[] pixels = [0, 0, 0, 64, 64, 64, 128, 128, 128, 192, 192, 192];
        var original = pixels.ToArray();
        var measured = RgbRegionMeasurements.Measure(pixels, 2, 2, [1, 1, 1, 1]);
        measured.Should().Succeed();
        measured.Value.PixelCount.Should().Be(4);
        measured.Value.Red.Value.Mean.Should().Be(96);
        measured.Value.Red.Value.Variance.Should().Be(5120);
        measured.Value.Red.Value.EntropyBits.Should().Be(2);
        measured.Value.Red.Value.MeanNeighborGradient.Should().Be(96);
        measured.Value.RedGreenCorrelation.Value.Should().BeApproximately(1, 1e-9);
        pixels.Should().Equal(original);
        var empty = RgbRegionMeasurements.Measure(pixels, 2, 2, [0, 0, 0, 0]).Value;
        empty.PixelCount.Should().Be(0);
        empty.Red.HasValue.Should().BeFalse();
        empty.RedGreenCorrelation.HasValue.Should().BeFalse();
    }
}
