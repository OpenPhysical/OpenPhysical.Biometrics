using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using OpenPhysical.Biometrics.Core.Abstractions;
using OpenPhysical.Biometrics.Core.Domain.Common;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Imaging;
using OpenPhysical.Biometrics.Infrastructure.Services;
using OpenPhysical.Biometrics.Tests.Common;

namespace OpenPhysical.Biometrics.Infrastructure.Tests.Services;

/// <summary>
/// Tests for the FacialImageEncoderService using functional programming patterns
/// </summary>
[TestFixture]
internal class FacialImageEncoderServiceTests : BiometricIntegrationTestBase
{
    private byte[] _validImageData = null!;
    private byte[] _invalidImageData = null!;
    private FacialImageEncoderService _encoderService = null!;

    /// <inheritdoc/>
    [OneTimeSetUp]
    public override void OneTimeSetUp()
    {
        base.OneTimeSetUp();

        // Load test image data - use the actual generic_guy.png
        var testImagePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "tests",
            "sample_images",
            "generic_guy.png"
        );

        if (!File.Exists(testImagePath))
        {
            throw new FileNotFoundException($"Required test image not found: {testImagePath}");
        }

        _validImageData = File.ReadAllBytes(testImagePath);
        Console.WriteLine($"Loaded test image: {testImagePath} ({_validImageData.Length} bytes)");

        _invalidImageData = new byte[] { 1, 2, 3, 4, 5 }; // Invalid image data

        // Create encoder service with DI services
        _encoderService = ServiceProvider.GetRequiredService<FacialImageEncoderService>();
    }

    /// <summary>
    /// Tests ProcessAsync with default options using functional approach
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithValidImage_ReturnsSuccessResult()
    {
        // Act
        var result = await _encoderService.ProcessAsync(_validImageData);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ImageData.Should().NotBeEmpty();
        result.Value.Metadata.OutputDimensions.Width.Should().Be(420);
        result.Value.Metadata.OutputDimensions.Height.Should().Be(560);
        result.Value.Metadata.FaceConfidence.Should().BeGreaterThan(0.7f);
        result.Value.Metadata.FileSize.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Tests ProcessAsync with invalid image data returns failure
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithInvalidImage_ReturnsFailureResult()
    {
        // Act
        var result = await _encoderService.ProcessAsync(_invalidImageData);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid image data");
    }

    /// <summary>
    /// Tests ProcessAsync with specific processing options
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithCustomOptions_ReturnsExpectedResult()
    {
        // Arrange
        var options = ProcessingOptions.PivHigh with
        {
            MinFaceConfidence = 0.85f,
            MaxRetries = 1,
        };

        // Act
        var result = await _encoderService.ProcessAsync(_validImageData, options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.FaceConfidence.Should().BeGreaterThanOrEqualTo(0.85f);
    }

    /// <summary>
    /// Tests ProcessAsync with different output formats
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithJpegFormat_ReturnsJpegOutput()
    {
        // Act
        var result = await _encoderService.ProcessAsync(
            _validImageData,
            ProcessingOptions.PivBalanced,
            "jpeg",
            85
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ImageData.Should().NotBeEmpty();

        // Verify JPEG signature (should start with FF D8)
        result.Value.ImageData[0].Should().Be(0xFF);
        result.Value.ImageData[1].Should().Be(0xD8);
    }

    /// <summary>
    /// Tests ProcessAsync with PNG format
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithPngFormat_ReturnsPngOutput()
    {
        // Act
        var result = await _encoderService.ProcessAsync(
            _validImageData,
            ProcessingOptions.PivBalanced,
            "png",
            85
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ImageData.Should().NotBeEmpty();

        // Verify PNG signature
        result.Value.ImageData[0].Should().Be(0x89);
        result.Value.ImageData[1].Should().Be(0x50);
        result.Value.ImageData[2].Should().Be(0x4E);
        result.Value.ImageData[3].Should().Be(0x47);
    }

    /// <summary>
    /// Tests ProcessAsync with TWIC-specific options
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithTwicOptions_ReturnsCompactResult()
    {
        // Act
        var result = await _encoderService.ProcessAsync(_validImageData, ProcessingOptions.TwicMax);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ImageData.Length.Should().BeLessThanOrEqualTo(14000); // TWIC size constraint
        result.Value.Metadata.FileSize.Should().BeLessThanOrEqualTo(14000);
    }

    /// <summary>
    /// Tests ProcessAsync with minimal size options
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithMinimalOptions_ReturnsVeryCompactResult()
    {
        // Act
        var result = await _encoderService.ProcessAsync(_validImageData, ProcessingOptions.Minimal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ImageData.Length.Should().BeLessThanOrEqualTo(10000); // Minimal size constraint
        result.Value.Metadata.FileSize.Should().BeLessThanOrEqualTo(10000);
    }

    /// <summary>
    /// Tests ProcessAsync with archival quality options
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithArchivalOptions_ReturnsHighQualityResult()
    {
        // Act
        var result = await _encoderService.ProcessAsync(
            _validImageData,
            ProcessingOptions.Archival
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.FaceConfidence.Should().BeGreaterThan(0.9f); // Higher confidence for archival
        result.Value.ImageData.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tests error handling when face detection fails
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithLowConfidenceOptions_ReturnsFailureWhenNoGoodFace()
    {
        // Use a known detector score so this checks filtering independently of model calibration.
        var detector = Substitute.For<IFaceDetector>();
        detector
            .DetectFacesAsync(Arg.Any<RgbImage>())
            .Returns(
                Task.FromResult(
                    Result.Success<IReadOnlyList<DetectedFace>>(
                        new[]
                        {
                            new DetectedFace(
                                FaceBox.Create(10, 10, 50, 50).Value,
                                0.9f,
                                Maybe<FaceLandmarks5>.None
                            ),
                        }
                    )
                )
            );
        using var service = new FacialImageEncoderService(
            detector,
            Substitute.For<IJpeg2000Encoder>(),
            NullLogger<FacialImageEncoderService>.Instance
        );
        var strictOptions = ProcessingOptions.PivBalanced with { MinFaceConfidence = 0.99f };
        var result = await service.ProcessAsync(_validImageData, strictOptions);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("confidence");
    }

    /// <summary>
    /// Tests that metadata contains expected information
    /// </summary>
    [Test]
    public async Task ProcessAsync_ResultMetadata_ContainsExpectedFields()
    {
        // Act
        var result = await _encoderService.ProcessAsync(_validImageData);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var metadata = result.Value.Metadata;
        metadata.OutputDimensions.Width.Should().Be(420);
        metadata.OutputDimensions.Height.Should().Be(560);
        metadata.FaceConfidence.Should().BeGreaterThan(0.0f);
        metadata.FileSize.Should().BeGreaterThan(0);
        metadata.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);
        metadata.CompressionRate.Should().BeGreaterThan(0.0f);
    }

    /// <summary>
    /// Tests that encoding strategy affects file size appropriately
    /// </summary>
    [Test]
    public async Task ProcessAsync_DifferentStrategies_ProduceDifferentFileSizes()
    {
        // Arrange
        var minimalResult = await _encoderService.ProcessAsync(
            _validImageData,
            ProcessingOptions.Minimal
        );
        var highQualityResult = await _encoderService.ProcessAsync(
            _validImageData,
            ProcessingOptions.PivHigh
        );

        // Assert
        minimalResult.IsSuccess.Should().BeTrue();
        highQualityResult.IsSuccess.Should().BeTrue();

        // Minimal should be smaller than high quality
        minimalResult
            .Value.ImageData.Length.Should()
            .BeLessThanOrEqualTo(highQualityResult.Value.ImageData.Length);
    }
}
