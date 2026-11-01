// SPDX-License-Identifier: GPL-2.0-or-later
using System.Drawing;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Imaging.Tests;

/// <summary>Checks decoding from memory, streams, and files, including malformed input.</summary>
[TestFixture]
public sealed class RgbImageDecoderTests
{
    /// <summary>Rejects empty input and unrecognised encodings.</summary>
    [Test]
    public void EmptyAndUnrecognisedInputsAreRejected()
    {
        using (new AssertionScope())
        {
            RgbImageDecoder
                .Load(ReadOnlyMemory<byte>.Empty)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.EncodedSizeInvalid);
            RgbImageDecoder
                .Load(new byte[] { 1, 2, 3 })
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.FormatUnsupported);
        }
    }

    /// <summary>Fails closed on truncated PNG data.</summary>
    [Test]
    public void TruncatedPngIsRejected()
    {
        using var image = TestImages.Pattern();
        var png = TestImages.Png(image);
        var result = RgbImageDecoder.Load(png.AsMemory(0, png.Length / 2));
        result
            .Should()
            .Fail()
            .Which.Code.Should()
            .BeOneOf(ImagingErrorCodes.DecodeFailed, ImagingErrorCodes.FormatUnsupported);
    }

    /// <summary>Decodes from streams and leaves the caller-owned stream open.</summary>
    [Test]
    public void StreamsDecodeAndStayOpen()
    {
        using var image = TestImages.Solid(3, 3, Color.Green);
        using var stream = new MemoryStream(TestImages.Png(image));
        using var decoded = RgbImageDecoder.Load(stream).Value;
        using (new AssertionScope())
        {
            decoded.At(1, 1).Should().Be(new RgbaPixel(0, 128, 0, 255));
            stream.CanRead.Should().BeTrue();
        }
    }

    /// <summary>Reports an empty stream as an invalid size.</summary>
    [Test]
    public void EmptyStreamsAreRejected()
    {
        using var stream = new MemoryStream();
        RgbImageDecoder
            .Load(stream)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(ImagingErrorCodes.EncodedSizeInvalid);
    }

    /// <summary>Reports missing files as IO failures for both sync and async loaders.</summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task MissingFilesFailWithIoCode()
    {
        var missing = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            Guid.NewGuid().ToString("N")
        );
        var async = await RgbImageDecoder.LoadAsync(missing);
        using (new AssertionScope())
        {
            RgbImageDecoder
                .Load(missing)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(ImagingErrorCodes.IoFailure);
            async.Should().Fail().Which.Code.Should().Be(ImagingErrorCodes.IoFailure);
        }
    }

    /// <summary>Loads files asynchronously and honours cancellation.</summary>
    /// <returns>A task that completes when the assertions finish.</returns>
    [Test]
    public async Task AsyncLoadDecodesFilesAndObservesCancellation()
    {
        using var image = TestImages.Pattern();
        var path = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            Guid.NewGuid().ToString("N") + ".png"
        );
        (await image.SaveAsPngAsync(path)).Should().Succeed();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var loaded = await RgbImageDecoder.LoadAsync(path);
        var stopped = await RgbImageDecoder.LoadAsync(path, cancelled.Token);
        File.Delete(path);
        using (new AssertionScope())
        {
            loaded
                .Should()
                .Succeed()
                .Which.CopyRgbBytes()
                .Value.Should()
                .Equal(image.CopyRgbBytes().Value);
            stopped.Should().Fail().Which.Code.Should().Be(ImagingErrorCodes.Cancelled);
        }
        loaded.Value.Dispose();
    }
}
