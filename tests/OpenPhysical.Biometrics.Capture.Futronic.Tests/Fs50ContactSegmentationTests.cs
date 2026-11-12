// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50ContactSegmentationTests
{
    [Test]
    public void TwoSeparateSubstantialContactsAreCountedAndOnlyTheLargestMaskIsRetained()
    {
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        _ = Paint(pixels, 80, 200, 160, 240);
        _ = Paint(pixels, 500, 200, 100, 160);

        var contact = Fs50ContactAnalysis.Analyze(pixels).Value;

        contact.ComponentCount.Should().Be(2);
        contact.Mask[300 * 800 + 160].Should().Be(1);
        contact.Mask[280 * 800 + 550].Should().Be(0);
    }

    [Test]
    public void ReusedScratchCannotLeakContactFromThePreviousFrame()
    {
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        _ = Paint(pixels, 300, 200, 160, 240);
        Fs50ContactAnalysis.Analyze(pixels).Value.ComponentCount.Should().Be(1);

        var clean = Fs50ContactAnalysis
            .Analyze(Enumerable.Repeat((byte)255, pixels.Length).ToArray())
            .Value;

        clean.ComponentCount.Should().Be(0);
        clean.Mask.Should().OnlyContain(value => value == 0);
    }

    [Test]
    public void WarmedSegmentationBoundsManagedAllocationPerFrame()
    {
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        for (var i = 0; i < 3; i++)
            _ = Fs50ContactAnalysis.Analyze(pixels);
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 5; i++)
            _ = Fs50ContactAnalysis.Analyze(pixels);

        ((GC.GetAllocatedBytesForCurrentThread() - before) / 5).Should().BeLessThan(4_000_000);
    }

    private static bool Paint(byte[] pixels, int left, int top, int width, int height)
    {
        for (var y = top; y < top + height; y++)
        for (var x = left; x < left + width; x++)
            pixels[y * 800 + x] = x % 8 < 4 ? (byte)170 : (byte)230;
        return true;
    }
}
