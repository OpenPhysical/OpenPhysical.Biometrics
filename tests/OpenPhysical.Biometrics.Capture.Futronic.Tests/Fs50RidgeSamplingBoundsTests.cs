// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50RidgeSamplingBoundsTests
{
    [Test]
    public void ExactlyCoveredContactBoundariesUseTheExistingGlobalSamplingGrid()
    {
        var pixels = new byte[800 * 750];
        for (var y = 0; y < 750; y++)
        for (var x = 0; x < 800; x++)
        {
            var amplitude = x is >= 277 and < 533 && y is >= 182 and < 502 ? 10 : 3;
            pixels[y * 800 + x] = (byte)
                Math.Round(215 + amplitude * Math.Sin(2 * Math.PI * (x - 13) / 8));
        }
        var observed = Fs50ContactAnalysis.Analyze(pixels).Value.Diagnostics;
        Coverage(observed.CandidateMask.AsSpan(), 280, 360).Should().Be(768);
        Coverage(observed.CandidateMask.AsSpan(), 408, 184).Should().Be(768);
        observed.RidgeSupportMask[360 * 800 + 276].Should().Be(1);
        observed.RidgeSupportMask[180 * 800 + 408].Should().Be(1);
    }

    [Test]
    public void ActualRightAndBottomFrameWindowsRetainTheirRidgeEvidence()
    {
        var pixels = Plane(0, false);
        var observed = Fs50ContactAnalysis.Analyze(pixels).Value;
        observed.Diagnostics.RidgeSupportMask[400 * 800 + 784].Should().Be(1);
        observed.Diagnostics.RidgeSupportMask[736 * 800 + 400].Should().Be(1);
    }

    [TestCase(20)]
    [TestCase(70)]
    public void WidePhaseAlignmentStaysInsideItsActualSourceFootprint(int angle)
    {
        var pixels = Plane(angle, true);
        var original = pixels.ToArray();
        var policy = Fs50EvidencePolicy
            .Create(Fs50EvidencePolicy.Default.Options with { MaximumParallelShift = 8 })
            .Value;
        var result = Fs50ContactAnalysis.Analyze(pixels, policy);
        result.Should().Succeed();
        var support = result.Value.Diagnostics.RidgeSupportMask;
        support.Count(value => value != 0).Should().BeGreaterThan(4000);
        for (var y = 0; y < 750; y++)
        for (var x = 0; x < 800; x++)
            if (x < 24 || x > 775 || y < 24 || y > 727)
                support[y * 800 + x].Should().Be(0);
        pixels.Should().Equal(original);
    }

    private static int Coverage(ReadOnlySpan<byte> mask, int centerX, int centerY)
    {
        var count = 0;
        for (var y = centerY - 16; y < centerY + 16; y++)
        for (var x = centerX - 16; x < centerX + 16; x++)
            count += mask[y * 800 + x];
        return count;
    }

    private static byte[] Plane(int degrees, bool curved)
    {
        var pixels = new byte[800 * 750];
        var angle = degrees * Math.PI / 180;
        for (var y = 0; y < 750; y++)
        for (var x = 0; x < 800; x++)
        {
            var normal = x * Math.Cos(angle) + y * Math.Sin(angle);
            var tangent = -x * Math.Sin(angle) + y * Math.Cos(angle);
            var phase = curved ? 4 * Math.Cos(Math.PI * tangent / 16) : 0;
            var period = curved ? 16 : 8;
            pixels[y * 800 + x] = (byte)
                Math.Round(145 + 65 * Math.Sin(2 * Math.PI * (normal + phase) / period));
        }
        return pixels;
    }
}
