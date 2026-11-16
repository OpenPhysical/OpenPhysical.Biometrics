// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class CaptureRegionGeometryTests
{
    [Test]
    public void OwnedMaskGeometryUsesOnlyMaskSamplesAndPreservesUnalignedOverlap()
    {
        var mask = new byte[100];
        mask[22] = mask[23] = mask[32] = mask[33] = 1;
        var region = CaptureRegionGeometry
            .Create(CaptureGeometry.Create(10, 10).Value, mask.AsSpan())
            .Value;
        region.Area.Should().Be(4);
        region.CenterX.Should().Be(2.5);
        region.CenterY.Should().Be(2.5);
        region.Diagonal.Should().BeApproximately(Math.Sqrt(8), 0.000001);
        Array.Clear(mask);
        region.Mask[22].Should().Be(1);
        var shifted = region.Translate(1, 0).Value;
        var other = CaptureRegionGeometry.Create(region.Geometry, shifted).Value;
        region.IntersectionOverUnion(other).Value.Should().BeApproximately(1d / 3, 0.000001);
        region.IntersectionOverUnion(other, 1, 0).Value.Should().Be(1);
        region.DisplacementFrom(other).Value.Should().Be(1);
    }

    [Test]
    public void InvalidGeometryOrMaskIsRejectedBeforeMeasurement()
    {
        var geometry = CaptureGeometry.Create(10, 10).Value;
        CaptureRegionGeometry
            .Create(geometry, new byte[99].AsSpan())
            .Error.Code.Should()
            .Be(CaptureErrorCodes.InvalidArgument);
        var mask = new byte[100];
        mask[0] = 2;
        CaptureRegionGeometry
            .Create(geometry, mask.AsSpan())
            .Error.Code.Should()
            .Be(CaptureErrorCodes.InvalidArgument);
    }
}
