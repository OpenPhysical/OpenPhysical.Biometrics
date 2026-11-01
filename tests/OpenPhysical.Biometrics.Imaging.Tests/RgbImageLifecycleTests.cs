// SPDX-License-Identifier: GPL-2.0-or-later
using System.Drawing;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Imaging.Tests;

/// <summary>Checks disposal and ownership transfer through clone transforms.</summary>
[TestFixture]
public sealed class RgbImageLifecycleTests
{
    /// <summary>Clears retained managed spans on disposal and tolerates repeated disposal.</summary>
    [Test]
    public void DisposalClearsManagedRows()
    {
        var image = TestImages.Solid(2, 1, Color.White);
        image.TryGetRowSpan(0, out var row).Should().BeTrue();
        image.Dispose();
        image.Dispose();
        using (new AssertionScope())
        {
            row[0].R.Should().Be(0);
            row[0].A.Should().Be(0);
            image.TryGetRowSpan(0, out _).Should().BeFalse();
        }
    }

    /// <summary>Reports a disposed failure from every operation instead of throwing.</summary>
    [Test]
    public void DisposedImagesRejectEveryOperation()
    {
        var image = TestImages.Solid(4, 4, Color.White);
        using var other = TestImages.Solid(1, 1, Color.Red);
        image.Dispose();
        using var stream = new MemoryStream();
        using (new AssertionScope())
        {
            Failed(image.Clone());
            Failed(image.GetPixel(0, 0));
            Failed(image.CopyRgbBytes());
            Failed(image.CopyRgbaBytes());
            Failed(image.Crop(new Rectangle(0, 0, 1, 1)));
            Failed(image.Resize(2, 2));
            Failed(image.Rotate(90));
            Failed(image.FlipHorizontal());
            Failed(image.Pad(8, 8));
            Failed(image.Fill(Color.Red));
            Failed(image.Grayscale());
            Failed(image.GaussianBlur(1));
            Failed(image.BackgroundColor(Color.Red));
            Failed(image.DrawImage(other, Point.Empty));
            Failed(other.DrawImage(image, Point.Empty));
            image
                .SetPixel(0, 0, new(1))
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.Disposed);
            image
                .SaveAsPng(stream)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.Disposed);
            image
                .SaveAsJpeg(stream)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.Disposed);
            image
                .SaveAsTiff(stream)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.Disposed);
            stream.Length.Should().Be(0);
        }
    }

    /// <summary>Disposes the copy when a clone transform fails and leaves the original intact.</summary>
    [Test]
    public void FailedCloneTransformDisposesTheCopy()
    {
        using var image = TestImages.Solid(2, 2, Color.Red);
        List<RgbImage> observed = [];
        var result = image.Clone(copy =>
        {
            observed.Add(copy);
            return copy.Crop(new Rectangle(0, 0, 9, 9));
        });
        using (new AssertionScope())
        {
            result.Should().Fail().Which.Code.Should().Be(ImagingErrorCodes.CropOutOfRange);
            observed.Should().ContainSingle().Which.IsDisposed.Should().BeTrue();
            image.IsDisposed.Should().BeFalse();
            image.At(0, 0).R.Should().Be(255);
        }
    }

    /// <summary>Returns the transformed copy and keeps the original unchanged.</summary>
    [Test]
    public void SuccessfulCloneTransformLeavesTheOriginalUntouched()
    {
        using var image = TestImages.Pattern();
        using var cropped = image.Clone(copy => copy.Crop(new Rectangle(1, 0, 2, 2))).Value;
        using (new AssertionScope())
        {
            (cropped.Width, cropped.Height).Should().Be((2, 2));
            (image.Width, image.Height).Should().Be((3, 2));
        }
    }

    private static void Failed<T>(Result<T, ImagingError> result) =>
        result.Should().Fail().Which.Code.Should().Be(ImagingErrorCodes.Disposed);
}
