// SPDX-License-Identifier: GPL-2.0-or-later
using System.Drawing;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Imaging.Tests;

/// <summary>Checks compositing and colour edits.</summary>
[TestFixture]
public sealed class RgbImageCompositingTests
{
    /// <summary>Blends an overlay with the requested additional opacity.</summary>
    [Test]
    public void OverlayHonoursOpacity()
    {
        using var image = TestImages.Solid(4, 4, Color.White);
        using var overlay = TestImages.Solid(2, 2, Color.Red);
        image.DrawImage(overlay, new Point(1, 1), 0.5f).Should().Succeed();
        using (new AssertionScope())
        {
            image.At(0, 0).Should().Be(new RgbaPixel(255, 255, 255, 255));
            image.At(1, 1).R.Should().Be(255);
            image.At(1, 1).G.Should().BeInRange(120, 135);
            image.At(3, 3).G.Should().Be(255);
        }
    }

    /// <summary>Rejects opacity values outside zero through one.</summary>
    /// <param name="opacity">Invalid opacity.</param>
    [TestCase(-0.1f)]
    [TestCase(1.1f)]
    [TestCase(float.NaN)]
    public void InvalidOpacityFails(float opacity)
    {
        using var image = TestImages.Solid(2, 2, Color.White);
        using var overlay = TestImages.Solid(1, 1, Color.Red);
        image
            .DrawImage(overlay, Point.Empty, opacity)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(ImagingErrorCodes.OpacityOutOfRange);
    }

    /// <summary>Composites transparent pixels over the background colour.</summary>
    [Test]
    public void BackgroundShowsThroughTransparency()
    {
        using var image = RgbImage.Create(2, 2).Value;
        image.BackgroundColor(Color.Blue).Should().Succeed();
        image.At(1, 1).Should().Be(new RgbaPixel(0, 0, 255, 255));
    }

    /// <summary>Fills the whole image or only a rectangle.</summary>
    [Test]
    public void FillCoversTheRequestedRegion()
    {
        using var image = TestImages.Solid(3, 3, Color.White);
        image.Fill(Color.Red, new Rectangle(1, 1, 1, 2)).Should().Succeed();
        using (new AssertionScope())
        {
            image.At(1, 2).Should().Be(new RgbaPixel(255, 0, 0));
            image.At(0, 0).Should().Be(new RgbaPixel(255));
            image.Fill(Color.Black).Should().Succeed();
            image.At(2, 2).Should().Be(new RgbaPixel(0));
        }
    }

    /// <summary>Rejects fill rectangles outside the image.</summary>
    [Test]
    public void FillOutsideTheImageFails()
    {
        using var image = TestImages.Solid(3, 3, Color.White);
        using (new AssertionScope())
        {
            image
                .Fill(Color.Red, new Rectangle(2, 2, 2, 1))
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.FillOutOfRange);
            image.Fill(Color.Red, new Rectangle(-1, 0, 1, 1)).Should().Fail();
            image.Fill(Color.Red, new Rectangle(0, 0, -1, 1)).Should().Fail();
        }
    }

    /// <summary>Converts to BT.709 luma while preserving alpha.</summary>
    [Test]
    public void GrayscaleUsesBt709AndKeepsAlpha()
    {
        using var image = RgbImage.Create(1, 1, new RgbaPixel(255, 0, 0, 77)).Value;
        image.Grayscale().Should().Succeed();
        image.At(0, 0).Should().Be(new RgbaPixel(54, 54, 54, 77));
    }
}
