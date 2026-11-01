// SPDX-License-Identifier: GPL-2.0-or-later
using System.Drawing;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Imaging.Tests;

/// <summary>Checks image construction, pixel access, and raw sample conversion.</summary>
[TestFixture]
public sealed class RgbImageCreationTests
{
    /// <summary>Rejects invalid geometry before allocating an image.</summary>
    /// <param name="width">Invalid input width.</param>
    /// <param name="height">Invalid input height.</param>
    [TestCase(0, 10)]
    [TestCase(-1, 10)]
    [TestCase(10, 0)]
    [TestCase(int.MaxValue, int.MaxValue)]
    public void InvalidGeometryIsRejectedBeforeAllocation(int width, int height)
    {
        using (new AssertionScope())
        {
            RgbImage
                .Create(width, height)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.InvalidDimensions);
            RgbImage.Create(width, height, Color.Red).Should().Fail();
            RgbImage.Create(width, height, new RgbaPixel(1)).Should().Fail();
            RgbImageBytes.FromRgbBytes([], width, height).Should().Fail();
        }
    }

    /// <summary>Fills every pixel with the requested background.</summary>
    [Test]
    public void BackgroundConstructorsFillEveryPixel()
    {
        using var colored = TestImages.Solid(2, 2, Color.Red);
        using var raw = RgbImage.Create(2, 2, new RgbaPixel(9, 8, 7, 6)).Value;
        using var transparent = RgbImage.Create(1, 1).Value;
        using (new AssertionScope())
        {
            colored.At(1, 1).Should().Be(new RgbaPixel(255, 0, 0, 255));
            raw.At(0, 1).Should().Be(new RgbaPixel(9, 8, 7, 6));
            transparent.At(0, 0).Should().Be(default(RgbaPixel));
            transparent.Bounds.Should().Be(new Rectangle(0, 0, 1, 1));
        }
    }

    /// <summary>Copies input pixels and keeps cloned images independent.</summary>
    [Test]
    public void InputAndClonesHaveIndependentStorage()
    {
        byte[] input = [255, 0, 0, 0, 0, 255];
        using var image = RgbImageBytes.FromRgbBytes(input, 2, 1).Value;
        using var clone = image.Clone().Value;
        Array.Clear(input);
        image.SetPixel(0, 0, new(0, 255, 0)).Should().Succeed();
        using (new AssertionScope())
        {
            clone.At(0, 0).R.Should().Be(255);
            image.At(0, 0).G.Should().Be(255);
            clone
                .CopyRgbBytes()
                .Should()
                .Succeed()
                .Which.Should()
                .Equal(new byte[] { 255, 0, 0, 0, 0, 255 });
        }
    }

    /// <summary>Rejects raw RGB buffers whose length differs from the dimensions.</summary>
    [Test]
    public void RawRgbLengthMustMatchDimensions() =>
        RgbImageBytes
            .FromRgbBytes(new byte[5], 2, 1)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(ImagingErrorCodes.RgbLengthMismatch);

    /// <summary>Copies RGB and RGBA samples in row-major order.</summary>
    [Test]
    public void SampleCopiesAreRowMajor()
    {
        using var image = TestImages.Pattern();
        using (new AssertionScope())
        {
            image.CopyRgbBytes().Should().Succeed().Which.Should().HaveCount(18);
            image.CopyRgbaBytes().Should().Succeed().Which.Should().HaveCount(24);
            image.CopyRgbaBytes().Value[..4].Should().Equal(new byte[] { 0, 0, 0, 255 });
            image.CopyRgbaBytes().Value[20..].Should().Equal(new byte[] { 160, 100, 3, 255 });
        }
    }

    /// <summary>Rejects pixel coordinates outside the image for both reads and writes.</summary>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    [TestCase(2, 0)]
    [TestCase(0, 1)]
    public void PixelAccessOutsideTheImageFails(int x, int y)
    {
        using var image = TestImages.Solid(2, 1, Color.White);
        using (new AssertionScope())
        {
            image
                .GetPixel(x, y)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.PixelOutOfRange);
            image
                .SetPixel(x, y, new(1))
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.PixelOutOfRange);
        }
    }

    /// <summary>Exposes managed rows only for rows that exist.</summary>
    [Test]
    public void RowSpansExistOnlyWithinTheImage()
    {
        using var image = TestImages.Pattern();
        using (new AssertionScope())
        {
            image.TryGetRowSpan(1, out var row).Should().BeTrue();
            row.Length.Should().Be(3);
            row[2].Should().Be(new RgbaPixel(160, 100, 3));
            image.TryGetRowSpan(2, out var missing).Should().BeFalse();
            missing.IsEmpty.Should().BeTrue();
            image.TryGetRowSpan(-1, out _).Should().BeFalse();
        }
    }
}
