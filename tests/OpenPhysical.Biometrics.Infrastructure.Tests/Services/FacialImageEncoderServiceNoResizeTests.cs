using AwesomeAssertions;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Infrastructure.Services;
using OpenPhysical.Biometrics.Tests.Common;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace OpenPhysical.Biometrics.Infrastructure.Tests.Services;

/// <summary>
/// Tests for the new no-resize functionality in FacialImageEncoderService using functional approach
/// </summary>
[TestFixture]
public class FacialImageEncoderServiceNoResizeTests : BiometricIntegrationTestBase
{
    private FacialImageEncoderService _encoderService = null!;

    [OneTimeSetUp]
    public override void OneTimeSetUp()
    {
        base.OneTimeSetUp();
        _encoderService = ServiceProvider.GetRequiredService<FacialImageEncoderService>();
    }

    [Test]
    public async Task ProcessWithoutFaceDetectionAsync_WithJpegFormat_ShouldPreserveOriginalDimensions()
    {
        // Arrange
        using var image = new Image<Rgba32>(800, 600);
        image.Mutate(x => x.BackgroundColor(Color.Blue));
        
        using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms);
        var imageData = ms.ToArray();

        // Act
        var result = await _encoderService.ProcessWithoutFaceDetectionAsync(
            imageData,
            options: null,
            outputFormat: "jpeg",
            jpegQuality: 85
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ImageData.Should().NotBeNull();
        result.Value.ImageData.Length.Should().BeGreaterThan(0);
        result.Value.Metadata.OutputDimensions.Width.Should().Be(800);
        result.Value.Metadata.OutputDimensions.Height.Should().Be(600);
        result.Value.Metadata.AdditionalData["OutputFormat"].Should().Be("JPEG");
        result.Value.Metadata.AdditionalData["ProcessingMode"].Should().Be("NoResize");
    }

    [Test]
    public async Task ProcessWithoutFaceDetectionAsync_WithPngFormat_ShouldProducePngOutput()
    {
        // Arrange
        using var image = new Image<Rgba32>(640, 480);
        image.Mutate(x => x.BackgroundColor(Color.Green));
        
        using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms);
        var imageData = ms.ToArray();

        // Act
        var result = await _encoderService.ProcessWithoutFaceDetectionAsync(
            imageData,
            outputFormat: "png"
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ImageData.Should().NotBeNull();
        
        // Verify PNG signature
        result.Value.ImageData.Length.Should().BeGreaterThan(8);
        result.Value.ImageData[0].Should().Be((byte)0x89); // PNG signature
        result.Value.ImageData[1].Should().Be((byte)0x50); // P
        result.Value.ImageData[2].Should().Be((byte)0x4E); // N
        result.Value.ImageData[3].Should().Be((byte)0x47); // G
        
        result.Value.Metadata.AdditionalData["OutputFormat"].Should().Be("PNG");
    }

    [Test]
    public async Task ProcessWithoutFaceDetectionAsync_WithTiffFormat_ShouldProduceTiffOutput()
    {
        // Arrange
        using var image = new Image<Rgba32>(320, 240);
        image.Mutate(x => x.BackgroundColor(Color.Red));
        
        using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms);
        var imageData = ms.ToArray();

        // Act
        var result = await _encoderService.ProcessWithoutFaceDetectionAsync(
            imageData,
            outputFormat: "tiff"
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ImageData.Should().NotBeNull();
        
        // Verify TIFF signature (little-endian or big-endian)
        result.Value.ImageData.Length.Should().BeGreaterThan(4);
        var isLittleEndian = result.Value.ImageData[0] == 0x49 && result.Value.ImageData[1] == 0x49;
        var isBigEndian = result.Value.ImageData[0] == 0x4D && result.Value.ImageData[1] == 0x4D;
        (isLittleEndian || isBigEndian).Should().BeTrue("Invalid TIFF signature");
        
        result.Value.Metadata.AdditionalData["OutputFormat"].Should().Be("TIFF");
    }

    [Test]
    public async Task ProcessWithoutFaceDetectionAsync_WithJp2Format_ShouldApplyDynamicRoi()
    {
        // Arrange
        using var image = new Image<Rgba32>(1000, 800);
        image.Mutate(x => x.BackgroundColor(Color.Blue));
        
        using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms);
        var imageData = ms.ToArray();

        var options = ProcessingOptions.PivBalanced with { EnableRoi = true };

        // Act
        var result = await _encoderService.ProcessWithoutFaceDetectionAsync(
            imageData,
            options: options,
            outputFormat: "jp2"
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ImageData.Should().NotBeNull();
        result.Value.ImageData.Length.Should().BeGreaterThan(0);
        result.Value.Metadata.OutputDimensions.Width.Should().Be(1000);
        result.Value.Metadata.OutputDimensions.Height.Should().Be(800);
        result.Value.Metadata.CompressionRate.Should().BeGreaterThan(0);
        result.Value.Metadata.AdditionalData["OutputFormat"].Should().Be("JP2");
    }

    [TestCase("jpeg")]
    [TestCase("jpg")]
    [TestCase("JPEG")]
    [TestCase("JPG")]
    public async Task ProcessWithoutFaceDetectionAsync_WithJpegVariants_ShouldAllWork(string format)
    {
        // Arrange
        using var image = new Image<Rgba32>(200, 200);
        using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms);
        var imageData = ms.ToArray();

        // Act
        var result = await _encoderService.ProcessWithoutFaceDetectionAsync(
            imageData,
            outputFormat: format,
            jpegQuality: 90
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ImageData.Should().NotBeNull();
        result.Value.ImageData.Length.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ProcessWithoutFaceDetectionAsync_ShouldSetFaceConfidenceToOne()
    {
        // Arrange
        using var image = new Image<Rgba32>(100, 100);
        using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms);
        var imageData = ms.ToArray();

        // Act
        var result = await _encoderService.ProcessWithoutFaceDetectionAsync(
            imageData,
            outputFormat: "png"
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.FaceConfidence.Should().Be(1.0f);
    }

    [Test]
    public async Task ProcessWithoutFaceDetectionAsync_ShouldHaveNoRotation()
    {
        // Arrange
        using var image = new Image<Rgba32>(100, 100);
        using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms);
        var imageData = ms.ToArray();

        // Act
        var result = await _encoderService.ProcessWithoutFaceDetectionAsync(
            imageData,
            outputFormat: "tiff"
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.RotationApplied.Should().Be(0f);
    }

    [TestCase("png")]
    [TestCase("tiff")]
    public async Task ProcessWithoutFaceDetectionAsync_WithDifferentFormats_ShouldSetCorrectMetadata(string format)
    {
        // Arrange
        using var image = new Image<Rgba32>(300, 400);
        using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms);
        var imageData = ms.ToArray();

        // Act
        var result = await _encoderService.ProcessWithoutFaceDetectionAsync(
            imageData,
            outputFormat: format
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.OutputDimensions.Width.Should().Be(300);
        result.Value.Metadata.OutputDimensions.Height.Should().Be(400);
        result.Value.Metadata.AdditionalData["ProcessingMode"].Should().Be("NoResize");
        result.Value.Metadata.AdditionalData["OutputFormat"].Should().Be(format.ToUpperInvariant());
    }

    [Test]
    public async Task ProcessWithoutFaceDetectionAsync_WithInvalidJpegQuality_ShouldReturnFailure()
    {
        // Arrange
        using var image = new Image<Rgba32>(100, 100);
        using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms);
        var imageData = ms.ToArray();

        // Act - Test quality too low
        var lowQualityResult = await _encoderService.ProcessWithoutFaceDetectionAsync(
            imageData,
            outputFormat: "jpeg",
            jpegQuality: 0
        );

        // Assert
        lowQualityResult.IsFailure.Should().BeTrue();
        lowQualityResult.Error.Should().Contain("JPEG quality must be between 1 and 100");

        // Act - Test quality too high
        var highQualityResult = await _encoderService.ProcessWithoutFaceDetectionAsync(
            imageData,
            outputFormat: "jpeg",
            jpegQuality: 101
        );

        // Assert
        highQualityResult.IsFailure.Should().BeTrue();
        highQualityResult.Error.Should().Contain("JPEG quality must be between 1 and 100");
    }

    [Test]
    public async Task ProcessWithoutFaceDetectionAsync_WithInvalidOptions_ShouldReturnFailure()
    {
        // Arrange
        using var image = new Image<Rgba32>(100, 100);
        using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms);
        var imageData = ms.ToArray();

        // Invalid options with negative confidence
        var invalidOptions = ProcessingOptions.PivBalanced with
        {
            MinFaceConfidence = -0.5f
        };

        // Act
        var result = await _encoderService.ProcessWithoutFaceDetectionAsync(
            imageData,
            options: invalidOptions,
            outputFormat: "png"
        );

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("MinFaceConfidence must be between 0 and 1");
    }

    [Test]
    public async Task ProcessWithoutFaceDetectionAsync_WithUnsupportedFormat_ShouldReturnFailure()
    {
        // Arrange
        using var image = new Image<Rgba32>(100, 100);
        using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms);
        var imageData = ms.ToArray();

        // Act
        var result = await _encoderService.ProcessWithoutFaceDetectionAsync(
            imageData,
            outputFormat: "bmp" // Unsupported format
        );

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unsupported output format: bmp");
    }

    [Test]
    public async Task ProcessWithoutFaceDetectionAsync_WithCorruptedImageData_ShouldReturnFailure()
    {
        // Arrange
        var corruptedData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 }; // Partial JPEG header

        // Act
        var result = await _encoderService.ProcessWithoutFaceDetectionAsync(
            corruptedData,
            outputFormat: "png"
        );

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid image data");
    }

    [Test]
    public async Task ProcessWithoutFaceDetectionAsync_WithVariousImageSizes_ShouldPreserveDimensions()
    {
        // Test various dimensions
        var testCases = new[]
        {
            (width: 100, height: 100),
            (width: 640, height: 480),
            (width: 1920, height: 1080),
            (width: 4096, height: 2160)
        };

        foreach (var (width, height) in testCases)
        {
            // Arrange
            using var image = new Image<Rgba32>(width, height);
            image.Mutate(x => x.BackgroundColor(Color.Gray));
            
            using var ms = new MemoryStream();
            await image.SaveAsJpegAsync(ms);
            var imageData = ms.ToArray();

            // Act
            var result = await _encoderService.ProcessWithoutFaceDetectionAsync(
                imageData,
                outputFormat: "jpeg",
                jpegQuality: 85
            );

            // Assert
            result.IsSuccess.Should().BeTrue($"Should process {width}x{height} image successfully");
            result.Value.Metadata.OutputDimensions.Width.Should().Be(width);
            result.Value.Metadata.OutputDimensions.Height.Should().Be(height);
        }
    }
}