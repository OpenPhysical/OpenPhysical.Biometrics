using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Infrastructure.Services;
using OpenPhysical.Biometrics.Tests.Common;

namespace OpenPhysical.Biometrics.Infrastructure.Tests.Services;

/// <summary>
/// Tests for FacialImageEncoderService with various image formats and configurations
/// </summary>
[TestFixture]
internal class FacialImageEncoderServiceFormatTests : BiometricIntegrationTestBase
{
    private static readonly string TestImagesPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "..",
        "tests",
        "sample_images"
    );

    private FacialImageEncoderService _encoderService = null!;

    [OneTimeSetUp]
    public override void OneTimeSetUp()
    {
        base.OneTimeSetUp();
        _encoderService = ServiceProvider.GetRequiredService<FacialImageEncoderService>();
    }

    /// <summary>
    /// Tests that JPEG format is processed correctly
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithJpegInput_ProcessesSuccessfully()
    {
        var jpegPath = Path.Combine(TestImagesPath, "generic_guy.jpg");
        var imageData = await File.ReadAllBytesAsync(jpegPath);

        var result = await _encoderService.ProcessAsync(imageData);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ImageData.Should().NotBeEmpty();
        result.Value.Metadata.OutputDimensions.Width.Should().Be(420);
        result.Value.Metadata.OutputDimensions.Height.Should().Be(560);
    }

    /// <summary>
    /// Tests that PNG format is processed correctly
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithPngInput_ProcessesSuccessfully()
    {
        var pngPath = Path.Combine(TestImagesPath, "generic_guy.png");
        var imageData = await File.ReadAllBytesAsync(pngPath);

        var result = await _encoderService.ProcessAsync(imageData);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ImageData.Should().NotBeEmpty();
        result.Value.Metadata.OutputDimensions.Width.Should().Be(420);
        result.Value.Metadata.OutputDimensions.Height.Should().Be(560);
    }

    /// <summary>
    /// Tests that TIFF format is processed correctly
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithTiffInput_ProcessesSuccessfully()
    {
        var tiffPath = Path.Combine(TestImagesPath, "generic_guy.tif");
        var imageData = await File.ReadAllBytesAsync(tiffPath);

        var result = await _encoderService.ProcessAsync(imageData);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ImageData.Should().NotBeEmpty();
        result.Value.Metadata.OutputDimensions.Width.Should().Be(420);
        result.Value.Metadata.OutputDimensions.Height.Should().Be(560);
    }

    /// <summary>
    /// Tests that JPEG 2000 format is NOT supported as input (ImageSharp limitation)
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithJpeg2000Input_ReturnsFailure()
    {
        var jp2Path = Path.Combine(TestImagesPath, "generic_guy.jp2");
        var imageData = await File.ReadAllBytesAsync(jp2Path);

        // ImageSharp doesn't support JP2 as input, only as output through our encoder
        var result = await _encoderService.ProcessAsync(imageData);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid image data");
    }

    /// <summary>
    /// Tests processing multiple formats in sequence
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithMultipleFormats_AllProcessSuccessfully()
    {
        var formats = new[] { "jpg", "png", "tif" }; // JP2 not supported as input
        var results = new List<Result<ProcessingResultDto>>();

        foreach (var format in formats)
        {
            var imagePath = Path.Combine(TestImagesPath, $"generic_guy.{format}");
            var imageData = await File.ReadAllBytesAsync(imagePath);

            var result = await _encoderService.ProcessAsync(imageData);
            results.Add(result);
        }

        results.Should().HaveCount(3);
        results
            .Should()
            .AllSatisfy(r =>
            {
                r.IsSuccess.Should().BeTrue();
                r.Value.Should().NotBeNull();
                r.Value.ImageData.Should().NotBeEmpty();
                r.Value.Metadata.OutputDimensions.Width.Should().Be(420);
                r.Value.Metadata.OutputDimensions.Height.Should().Be(560);
            });
    }

    /// <summary>
    /// Tests output format variations using functional approach
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithDifferentOutputFormats_ProducesCorrectFormat()
    {
        var pngPath = Path.Combine(TestImagesPath, "generic_guy.png");
        var imageData = await File.ReadAllBytesAsync(pngPath);

        // Test JPEG output
        var jpegResult = await _encoderService.ProcessAsync(
            imageData,
            ProcessingOptions.PivBalanced,
            "jpeg",
            85
        );
        jpegResult.IsSuccess.Should().BeTrue();
        jpegResult.Value.ImageData[0].Should().Be(0xFF); // JPEG signature
        jpegResult.Value.ImageData[1].Should().Be(0xD8);

        // Test PNG output
        var pngResult = await _encoderService.ProcessAsync(
            imageData,
            ProcessingOptions.PivBalanced,
            "png",
            85
        );
        pngResult.IsSuccess.Should().BeTrue();
        pngResult.Value.ImageData[0].Should().Be(0x89); // PNG signature
        pngResult.Value.ImageData[1].Should().Be(0x50);
        pngResult.Value.ImageData[2].Should().Be(0x4E);
        pngResult.Value.ImageData[3].Should().Be(0x47);

        // Test TIFF output
        var tiffResult = await _encoderService.ProcessAsync(
            imageData,
            ProcessingOptions.PivBalanced,
            "tiff",
            85
        );
        tiffResult.IsSuccess.Should().BeTrue();
        var isLittleEndian =
            tiffResult.Value.ImageData[0] == 0x49 && tiffResult.Value.ImageData[1] == 0x49;
        var isBigEndian =
            tiffResult.Value.ImageData[0] == 0x4D && tiffResult.Value.ImageData[1] == 0x4D;
        (isLittleEndian || isBigEndian).Should().BeTrue("Should have valid TIFF signature");
    }

    /// <summary>
    /// Tests that all formats produce consistent results
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithDifferentFormats_ProducesConsistentResults()
    {
        var formats = new[] { "jpg", "png", "tif" }; // JP2 not supported as input
        var results = new Dictionary<string, ProcessingResultDto>();

        foreach (var format in formats)
        {
            var imagePath = Path.Combine(TestImagesPath, $"generic_guy.{format}");
            var imageData = await File.ReadAllBytesAsync(imagePath);

            var result = await _encoderService.ProcessAsync(imageData);
            result.IsSuccess.Should().BeTrue($"Processing {format} should succeed");
            results[format] = result.Value;
        }

        // All should have same output dimensions
        var dimensions = results.Values.Select(r => r.Metadata.OutputDimensions).ToList();
        dimensions.Should().AllBeEquivalentTo(dimensions.First());

        // Face confidence should be similar across formats (within 5%)
        var confidences = results.Values.Select(r => r.Metadata.FaceConfidence).ToList();
        var avgConfidence = confidences.Average();
        confidences
            .Should()
            .AllSatisfy(c => Math.Abs(c - avgConfidence).Should().BeLessThan(0.05f));

        // Rotation should be very similar (within 0.1 degrees)
        var rotations = results.Values.Select(r => r.Metadata.RotationApplied).ToList();
        var avgRotation = rotations.Average();
        rotations.Should().AllSatisfy(r => Math.Abs(r - avgRotation).Should().BeLessThan(0.1f));
    }

    /// <summary>
    /// Tests error handling with corrupted image data using functional approach
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithCorruptedData_ReturnsFailure()
    {
        var corruptedData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 }; // Partial JPEG header

        var result = await _encoderService.ProcessAsync(corruptedData);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid image data");
    }

    /// <summary>
    /// Tests that unsupported format returns appropriate failure
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithUnsupportedFormat_ReturnsFailure()
    {
        var bmpHeader = new byte[] { 0x42, 0x4D, 0x00, 0x00, 0x00, 0x00 }; // BMP header (not supported by ImageSharp)

        var result = await _encoderService.ProcessAsync(bmpHeader);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid image data");
    }

    /// <summary>
    /// Tests JPEG quality settings produce different file sizes
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithDifferentJpegQuality_ProducesDifferentSizes()
    {
        var pngPath = Path.Combine(TestImagesPath, "generic_guy.png");
        var imageData = await File.ReadAllBytesAsync(pngPath);

        // Test low quality JPEG
        var lowQualityResult = await _encoderService.ProcessAsync(
            imageData,
            ProcessingOptions.PivBalanced,
            "jpeg",
            30
        );

        // Test high quality JPEG
        var highQualityResult = await _encoderService.ProcessAsync(
            imageData,
            ProcessingOptions.PivBalanced,
            "jpeg",
            95
        );

        lowQualityResult.IsSuccess.Should().BeTrue();
        highQualityResult.IsSuccess.Should().BeTrue();

        // Low quality should be smaller
        lowQualityResult
            .Value.ImageData.Length.Should()
            .BeLessThan(highQualityResult.Value.ImageData.Length);
    }

    /// <summary>
    /// Tests format name case insensitivity
    /// </summary>
    [TestCase("jpeg")]
    [TestCase("JPEG")]
    [TestCase("Jpeg")]
    [TestCase("jpg")]
    [TestCase("JPG")]
    public async Task ProcessAsync_WithFormatNameVariations_AllWork(string format)
    {
        var pngPath = Path.Combine(TestImagesPath, "generic_guy.png");
        var imageData = await File.ReadAllBytesAsync(pngPath);

        var result = await _encoderService.ProcessAsync(
            imageData,
            ProcessingOptions.PivBalanced,
            format,
            85
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.ImageData.Should().NotBeEmpty();
        // All JPEG variants should produce JPEG output
        result.Value.ImageData[0].Should().Be(0xFF);
        result.Value.ImageData[1].Should().Be(0xD8);
    }

    /// <summary>
    /// Tests invalid output format handling
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithInvalidOutputFormat_ReturnsFailure()
    {
        var pngPath = Path.Combine(TestImagesPath, "generic_guy.png");
        var imageData = await File.ReadAllBytesAsync(pngPath);

        var result = await _encoderService.ProcessAsync(
            imageData,
            ProcessingOptions.PivBalanced,
            "bmp", // Unsupported format
            85
        );

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unsupported output format");
    }
}
