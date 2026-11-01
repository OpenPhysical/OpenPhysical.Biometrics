// SPDX-License-Identifier: GPL-2.0-or-later
using System.Drawing;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Imaging.Tests;

/// <summary>Checks geometric transforms and their failure codes.</summary>
[TestFixture]
public sealed class RgbImageTransformTests
{
    /// <summary>Preserves sample values when cropping and flipping pixel positions.</summary>
    [Test]
    public void CropAndReflectionPreservePixelCoordinates()
    {
        using var image = TestImages.Pattern();
        var expected = image.At(2, 1);
        image
            .Crop(new Rectangle(1, 0, 2, 2))
            .Bind(cropped => cropped.FlipHorizontal())
            .Should()
            .Succeed();
        using (new AssertionScope())
        {
            (image.Width, image.Height).Should().Be((2, 2));
            image.At(0, 1).Should().Be(expected);
        }
    }

    /// <summary>Rejects empty, negative, or overflowing crop rectangles and leaves the image unchanged.</summary>
    /// <param name="x">Rectangle left edge.</param>
    /// <param name="y">Rectangle top edge.</param>
    /// <param name="width">Rectangle width.</param>
    /// <param name="height">Rectangle height.</param>
    [TestCase(0, 0, 0, 1)]
    [TestCase(0, 0, 1, 0)]
    [TestCase(-1, 0, 1, 1)]
    [TestCase(0, -1, 1, 1)]
    [TestCase(2, 0, 2, 1)]
    [TestCase(0, 1, 1, 2)]
    [TestCase(int.MaxValue, 0, int.MaxValue, 1)]
    public void InvalidCropRectanglesFail(int x, int y, int width, int height)
    {
        using var image = TestImages.Pattern();
        using (new AssertionScope())
        {
            image
                .Crop(new(x, y, width, height))
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.CropOutOfRange);
            (image.Width, image.Height).Should().Be((3, 2));
        }
    }

    /// <summary>Expands a quarter-turn canvas to the exact transposed dimensions.</summary>
    [Test]
    public void QuarterTurnUsesExactExpandedCanvasDimensions()
    {
        using var image = TestImages.Pattern();
        image.Rotate(90).Should().Succeed();
        (image.Width, image.Height).Should().Be((2, 3));
    }

    /// <summary>Grows the canvas to retain a diagonally rotated image.</summary>
    [Test]
    public void DiagonalRotationExpandsTheCanvas()
    {
        using var image = TestImages.Solid(10, 10, Color.Red);
        image.Rotate(45).Should().Succeed();
        image.Width.Should().Be(15);
    }

    /// <summary>Rejects rotation angles that are not finite numbers.</summary>
    /// <param name="degrees">Invalid angle.</param>
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void NonFiniteRotationFails(float degrees)
    {
        using var image = TestImages.Pattern();
        image
            .Rotate(degrees)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(ImagingErrorCodes.RotationNotFinite);
    }

    /// <summary>Exercises native resize, centred padding, and background compositing.</summary>
    [Test]
    public void ResizeAndPadKeepTheRequestedGeometry()
    {
        using var image = TestImages.Solid(8, 4, Color.Red);
        var result = image
            .Resize(4, 4, fillCrop: true)
            .Bind(resized => resized.Pad(8, 8))
            .Bind(padded => padded.BackgroundColor(Color.White));
        using (new AssertionScope())
        {
            result.Should().Succeed();
            (image.Width, image.Height).Should().Be((8, 8));
            image.At(0, 0).G.Should().Be(255);
            image.At(4, 4).R.Should().Be(255);
            image.At(4, 4).G.Should().Be(0);
        }
    }

    /// <summary>Resizes to a size value and rejects invalid targets.</summary>
    [Test]
    public void ResizeAcceptsSizesAndRejectsInvalidDimensions()
    {
        using var image = TestImages.Solid(4, 4, Color.Red);
        using (new AssertionScope())
        {
            image
                .Resize(0, 4)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.InvalidDimensions);
            image.Resize(new Size(2, 6)).Should().Succeed();
            (image.Width, image.Height).Should().Be((2, 6));
        }
    }

    /// <summary>Rejects padding smaller than the image.</summary>
    [Test]
    public void PaddingMustContainTheImage()
    {
        using var image = TestImages.Pattern();
        image.Pad(2, 2).Should().Fail().Which.Code.Should().Be(ImagingErrorCodes.PaddingTooSmall);
    }

    /// <summary>Keeps dimensions when blurring and rejects unusable sigma values.</summary>
    /// <param name="sigma">Invalid blur sigma.</param>
    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(101f)]
    [TestCase(float.NaN)]
    public void InvalidBlurSigmaFails(float sigma)
    {
        using var image = TestImages.Solid(4, 4, Color.Red);
        using (new AssertionScope())
        {
            image
                .GaussianBlur(sigma)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.BlurSigmaOutOfRange);
            image.GaussianBlur(1).Should().Succeed();
            (image.Width, image.Height).Should().Be((4, 4));
        }
    }
}
