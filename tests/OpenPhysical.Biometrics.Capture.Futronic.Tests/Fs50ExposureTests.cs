// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50ExposureTests
{
    [TestCase(0)]
    [TestCase(128)]
    [TestCase(255)]
    public void UniformGlassDoesNotDriveExposure(int value)
    {
        var pixels = Enumerable.Repeat((byte)value, 800 * 750).ToArray();
        var contact = Fs50ContactAnalysis.Analyze(pixels).Value;
        contact.Present.Should().BeFalse();
        Fs50ExposureController.Next(128, contact, true, false).Should().Be(128);
    }

    [Test]
    public void SmallTexturedMarkIsRejected()
    {
        var pixels = Image(20, 20, 60, 140);
        Fs50ContactAnalysis.Analyze(pixels).Value.Present.Should().BeFalse();
    }

    [Test]
    public void ContactExposureIsBoundedAndHonorsExplicitLocks()
    {
        var pixels = Image(160, 240, 170, 230);
        var original = pixels.ToArray();
        var contact = Fs50ContactAnalysis.Analyze(pixels).Value;
        contact.Present.Should().BeTrue();
        // Saved prototype, OpenCV 5.0.0.93, synthetic stripes defined below.
        contact.Area.Should().Be(41298);
        contact.Contrast.Should().Be(85);
        contact.Signal.Should().Be(85);
        Fs50ExposureController.Next(100, contact, true, false).Should().Be(116);
        Fs50ExposureController.Next(255, contact, true, false).Should().Be(255);
        Fs50ExposureController.Next(100, contact, false, false).Should().Be(100);
        Fs50ExposureController.Next(100, contact, true, true).Should().Be(100);
        pixels.Should().Equal(original);
    }

    [Test]
    public void IncompleteImageFailsBeforeAnalysis() =>
        Fs50ContactAnalysis
            .Analyze(new byte[10])
            .Error.Code.Should()
            .Be(CaptureErrorCodes.UnsupportedGeometry);

    [Test]
    public void StrongContactReducesExposureWithinTheSameBound()
    {
        var contact = Fs50ContactAnalysis.Analyze(Image(160, 240, 40, 100)).Value;
        contact.Area.Should().Be(43506);
        contact.Contrast.Should().Be(215);
        contact.Signal.Should().Be(215);
        Fs50ExposureController.Next(255, contact, true, false).Should().Be(247);
    }

    private static byte[] Image(int width, int height, byte low, byte high)
    {
        var image = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        for (int y = 200; y < 200 + height; y++)
        for (int x = 300; x < 300 + width; x++)
            image[(y * 800) + x] = x % 8 < 4 ? low : high;
        return image;
    }
}
