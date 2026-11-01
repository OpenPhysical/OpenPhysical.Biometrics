// SPDX-License-Identifier: GPL-2.0-or-later
using System.Drawing;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Imaging.Tests;

/// <summary>Checks lossless interchange, JPEG output, files, and cancellation.</summary>
[TestFixture]
public sealed class RgbImageEncodingTests
{
    /// <summary>Preserves RGB pixels in both lossless formats used by the compatibility API.</summary>
    /// <param name="tiff">True selects baseline TIFF; false selects PNG.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void LosslessEncodingRoundTripsPixelSamples(bool tiff)
    {
        using var image = TestImages.Pattern();
        using var stream = new MemoryStream();
        (tiff ? image.SaveAsTiff(stream) : image.SaveAsPng(stream)).Should().Succeed();
        using var decoded = RgbImageDecoder.Load(stream.ToArray()).Value;
        using (new AssertionScope())
        {
            (decoded.Width, decoded.Height).Should().Be((3, 2));
            decoded.CopyRgbBytes().Value.Should().Equal(image.CopyRgbBytes().Value);
        }
    }

    /// <summary>Produces a real JPEG stream that the decoder can read.</summary>
    [Test]
    public void JpegEncodingUsesTheNativeCodec()
    {
        using var image = TestImages.Solid(16, 16, Color.Red);
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, 90).Should().Succeed();
        var bytes = stream.ToArray();
        using var decoded = RgbImageDecoder.Load(bytes).Value;
        using (new AssertionScope())
        {
            bytes[..2].Should().Equal(new byte[] { 0xFF, 0xD8 });
            (decoded.Width, decoded.Height).Should().Be((16, 16));
            decoded.At(8, 8).R.Should().BeGreaterThan(240);
        }
    }

    /// <summary>Rejects JPEG quality values outside one through 100 without writing.</summary>
    /// <param name="quality">Invalid quality.</param>
    [TestCase(0)]
    [TestCase(101)]
    public void InvalidJpegQualityFails(int quality)
    {
        using var image = TestImages.Pattern();
        using var stream = new MemoryStream();
        using (new AssertionScope())
        {
            image
                .SaveAsJpeg(stream, quality)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.QualityOutOfRange);
            stream.Length.Should().Be(0);
        }
    }

    /// <summary>Reports stream and file faults as IO failures.</summary>
    [Test]
    public void UnwritableDestinationsFailWithIoCode()
    {
        using var image = TestImages.Pattern();
        using var readOnly = new MemoryStream(new byte[8], writable: false);
        var missing = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            Guid.NewGuid().ToString("N"),
            "x.png"
        );
        using (new AssertionScope())
        {
            image
                .SaveAsPng(readOnly)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.IoFailure);
            image
                .SaveAsTiff(readOnly)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.IoFailure);
            image
                .SaveAsPng(missing)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.IoFailure);
            image
                .SaveAsJpeg(missing)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.IoFailure);
        }
    }

    /// <summary>Writes files that the path loader reads back losslessly.</summary>
    [Test]
    public void PngFilesRoundTrip()
    {
        using var image = TestImages.Pattern();
        var path = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            Guid.NewGuid().ToString("N") + ".png"
        );
        image.SaveAsPng(path).Should().Succeed();
        using var decoded = RgbImageDecoder.Load(path).Value;
        File.Delete(path);
        decoded.CopyRgbBytes().Value.Should().Equal(image.CopyRgbBytes().Value);
    }

    /// <summary>Completes async saves with results and observes cancellation up front.</summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task AsyncSavesReturnResultsAndObserveCancellation()
    {
        using var image = TestImages.Pattern();
        using var stream = new MemoryStream();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var done = await image.SaveAsPngAsync(stream);
        var jpeg = await image.SaveAsJpegAsync(stream, 80);
        var tiff = await image.SaveAsTiffAsync(stream);
        var stopped = await image.SaveAsPngAsync(stream, cancelled.Token);
        var stoppedFile = await image.SaveAsJpegAsync("unused.jpg", 85, cancelled.Token);
        using (new AssertionScope())
        {
            done.Should().Succeed();
            jpeg.Should().Succeed();
            tiff.Should().Succeed();
            stopped.Should().Fail().Which.Code.Should().Be(ImagingErrorCodes.Cancelled);
            stoppedFile.Should().Fail().Which.Code.Should().Be(ImagingErrorCodes.Cancelled);
        }
    }
}
