using System.Collections.Immutable;
using System.Drawing;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Core.Domain.Common;
using OpenPhysical.Biometrics.Imaging;
using OpenPhysical.Biometrics.Infrastructure.Services;

namespace OpenPhysical.Biometrics.Infrastructure.Tests.Services;

[TestFixture]
internal sealed class ImageGeometryRegressionTests
{
    [Test]
    public void WideLandmarkCropUsesLetterboxingBeforeInference()
    {
        using var source = new RgbImage(112, 56, Color.Red);
        using var prepared = OnnxLandmarkExtractor.PrepareModelInput(
            source,
            new Rectangle(0, 0, 112, 56)
        );
        Assert.Multiple(() =>
        {
            Assert.That(prepared.Pixels[56, 0].R, Is.Zero);
            Assert.That(prepared.Pixels[56, 28].R, Is.EqualTo(255));
            Assert.That(prepared.MapToSource(new Point2D(0, 28)), Is.EqualTo(new Point2D(0, 0)));
        });
    }

    [Test]
    public void LandmarkMappingUsesTheActualIntegerPaddingAndResize()
    {
        using var source = new RgbImage(137, 300, Color.Blue);
        using var prepared = OnnxLandmarkExtractor.PrepareModelInput(
            source,
            new Rectangle(0, 0, 137, 300)
        );
        var point = prepared.MapToSource(new Point2D(30, 0));
        Assert.That(point.X, Is.EqualTo(0).Within(0.0001));
        Assert.That(point.Y, Is.EqualTo(0).Within(0.0001));
    }

    [Test]
    public async Task RotationCanRetainTheOriginalCanvasDimensions()
    {
        using var source = new RgbImage(200, 100, Color.Red);
        using var bytes = new MemoryStream();
        source.SaveAsPng(bytes);
        var service = new ImageProcessingService(NullLogger<ImageProcessingService>.Instance);
        var result = await service.RotateForAlignmentAsync(
            bytes.ToArray().ToImmutableArray(),
            45,
            new RotationOptions(CropToOriginalSize: true)
        );
        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : null);
        using var rotated = RgbImage.Load(result.Value.ImageData.AsSpan());
        Assert.That(rotated.Width, Is.EqualTo(200));
        Assert.That(rotated.Height, Is.EqualTo(100));
    }
}
