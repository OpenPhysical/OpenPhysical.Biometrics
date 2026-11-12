// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50CurvedFaintRidgeTests
{
    [Test]
    public void CurvedFaintRidgesRetainUsableContactLikeTheStraightPixelControl()
    {
        using var rig = new WholeContactRig();
        rig.Learn().Should().BeTrue();
        foreach (var curved in new[] { false, true })
        {
            var pixels = Pixels(curved);
            var observed = rig.Observe(pixels);
            observed.Contact.Diagnostics.RidgeSupportArea.Should().BeGreaterThan(4000);
            observed.Contact.ComponentCount.Should().Be(1);
            observed.Proof.Qualified.Should().BeTrue();
            observed.Frame.Pixels.Should().Equal(pixels);
        }
    }

    private static byte[] Pixels(bool curved)
    {
        var pixels = Fs50EvidenceFrameFixture.Empty();
        for (var y = 165; y < 585; y++)
        for (var x = 260; x < 540; x++)
        {
            var phase = curved ? 1.5 * Math.Cos(Math.PI * y / 16) : 0;
            pixels[y * 800 + x] = (byte)
                Math.Round(215 + 10 * Math.Sin(2 * Math.PI * (x + phase) / 8));
        }
        return pixels;
    }
}
