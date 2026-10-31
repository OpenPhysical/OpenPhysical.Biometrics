using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using OpenPhysical.Biometrics.Core.Domain.Transformations;
using OpenPhysical.Biometrics.Infrastructure.Services;
using OpenPhysical.Biometrics.Tests.Common;

namespace OpenPhysical.Biometrics.Infrastructure.Tests.Services;

/// <summary>
/// Tests for FacialImageEncoderService parameter functionality (rotation, confidence)
/// </summary>
[TestFixture]
internal class FacialImageEncoderServiceParameterTests : BiometricIntegrationTestBase
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
    /// Tests processing with default 15 degree rotation limit
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithDefaultRotationLimit_Processes15DegreeRotation()
    {
        var imagePath = Path.Combine(TestImagesPath, "generic_guy_rotated_15.png");
        var imageData = await File.ReadAllBytesAsync(imagePath);

        var result = await _encoderService.ProcessAsync(imageData);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ImageData.Should().NotBeEmpty();
        // Should apply rotation close to 15 degrees (within tolerance)
        Math.Abs(result.Value.Metadata.RotationApplied).Should().BeGreaterThan(10f);
        Math.Abs(result.Value.Metadata.RotationApplied).Should().BeLessThanOrEqualTo(15f);
    }

    /// <summary>
    /// Tests that rotation beyond default limit is clamped
    /// </summary>
    [Test]
    public async Task ProcessAsync_With20DegreeRotation_ClampsToDefault15()
    {
        var imagePath = Path.Combine(TestImagesPath, "generic_guy_rotated_20.png");
        var imageData = await File.ReadAllBytesAsync(imagePath);

        var result = await _encoderService.ProcessAsync(imageData);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        // Rotation should be clamped to 15 degrees
        Math.Abs(result.Value.Metadata.RotationApplied).Should().BeLessThanOrEqualTo(15f);
    }

    /// <summary>
    /// Tests custom rotation limit
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithCustomRotationLimit_RespectsLimit()
    {
        var imagePath = Path.Combine(TestImagesPath, "generic_guy_rotated_10.png");
        var imageData = await File.ReadAllBytesAsync(imagePath);

        // Set custom 5 degree limit
        var options = ProcessingOptions.PivBalanced with
        {
            MaxRotationDegrees = 5.0f,
        };
        var result = await _encoderService.ProcessAsync(imageData, options);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        // Rotation should be clamped to 5 degrees
        Math.Abs(result.Value.Metadata.RotationApplied).Should().BeLessThanOrEqualTo(5f);
    }

    /// <summary>
    /// Tests negative rotation handling
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithNegativeRotation_HandlesCorrectly()
    {
        var imagePath = Path.Combine(TestImagesPath, "generic_guy_rotated_neg10.png");
        var imageData = await File.ReadAllBytesAsync(imagePath);

        var result = await _encoderService.ProcessAsync(imageData);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        // The rotation applied is to correct the image, so if the image is rotated -10 degrees,
        // the correction would be +10 degrees to make it level
        result.Value.Metadata.RotationApplied.Should().BePositive();
        Math.Abs(result.Value.Metadata.RotationApplied).Should().BeGreaterThan(5f);
    }

    /// <summary>
    /// Tests various rotation angles
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithVariousRotations_ProcessesCorrectly()
    {
        var rotations = new[] { 5, 10, 15, -10 };
        var results = new Dictionary<int, float>();

        foreach (var rotation in rotations)
        {
            var filename =
                rotation >= 0
                    ? $"generic_guy_rotated_{rotation}.png"
                    : $"generic_guy_rotated_neg{Math.Abs(rotation)}.png";
            var imagePath = Path.Combine(TestImagesPath, filename);
            var imageData = await File.ReadAllBytesAsync(imagePath);

            var result = await _encoderService.ProcessAsync(imageData);
            result
                .IsSuccess.Should()
                .BeTrue($"Processing should succeed for {rotation} degree rotation");
            results[rotation] = result.Value.Metadata.RotationApplied;
        }

        // Model estimates vary with resampling. Check correction direction and the configured
        // bound here; deterministic geometry tests cover the coordinate transform itself.
        results
            .Values.Should()
            .OnlyContain(value => float.IsFinite(value) && Math.Abs(value) <= 15f);
        results[5].Should().BeNegative();
        results[10].Should().BeNegative();
        results[15].Should().BeNegative();
        results[-10].Should().BePositive();
        Math.Abs(results[15]).Should().BeGreaterThanOrEqualTo(Math.Abs(results[5]));
    }

    /// <summary>
    /// Tests custom confidence threshold
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithHighConfidenceThreshold_ProcessesHighConfidenceFaces()
    {
        var imagePath = Path.Combine(TestImagesPath, "generic_guy.png");
        var imageData = await File.ReadAllBytesAsync(imagePath);

        // Set high confidence threshold
        var options = ProcessingOptions.PivBalanced with
        {
            MinFaceConfidence = 0.95f,
        };
        var result = await _encoderService.ProcessAsync(imageData, options);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Metadata.FaceConfidence.Should().BeGreaterThanOrEqualTo(0.95f);
    }

    /// <summary>
    /// Tests low confidence threshold
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithLowConfidenceThreshold_AcceptsLowerConfidenceFaces()
    {
        var imagePath = Path.Combine(TestImagesPath, "generic_guy.png");
        var imageData = await File.ReadAllBytesAsync(imagePath);

        // Set low confidence threshold
        var options = ProcessingOptions.PivBalanced with
        {
            MinFaceConfidence = 0.5f,
        };
        var result = await _encoderService.ProcessAsync(imageData, options);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        // Should process successfully even with lower threshold
        result.Value.ImageData.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tests combined rotation and confidence parameters
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithCombinedParameters_RespectsAllSettings()
    {
        var imagePath = Path.Combine(TestImagesPath, "generic_guy_rotated_10.png");
        var imageData = await File.ReadAllBytesAsync(imagePath);

        // Set custom parameters
        var options = ProcessingOptions.PivBalanced with
        {
            MaxRotationDegrees = 8.0f,
            MinFaceConfidence = 0.9f,
        };

        var result = await _encoderService.ProcessAsync(imageData, options);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        // Rotation should be limited to 8 degrees
        Math.Abs(result.Value.Metadata.RotationApplied).Should().BeLessThanOrEqualTo(8f);
        // Face confidence should meet threshold
        result.Value.Metadata.FaceConfidence.Should().BeGreaterThanOrEqualTo(0.9f);
    }

    /// <summary>
    /// Tests that presets maintain their specific settings
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithPresets_MaintainsPresetSettings()
    {
        var imagePath = Path.Combine(TestImagesPath, "generic_guy.png");
        var imageData = await File.ReadAllBytesAsync(imagePath);

        // Test archival preset (has high confidence requirement)
        var archivalResult = await _encoderService.ProcessAsync(
            imageData,
            ProcessingOptions.Archival
        );

        archivalResult.IsSuccess.Should().BeTrue();
        archivalResult.Value.Metadata.FaceConfidence.Should().BeGreaterThanOrEqualTo(0.95f); // Archival has 0.95 threshold

        // Test fast preset (has lower confidence requirement)
        var fastResult = await _encoderService.ProcessAsync(imageData, ProcessingOptions.Fast);
        fastResult.IsSuccess.Should().BeTrue();
        fastResult.Value.Should().NotBeNull(); // Fast has 0.7 threshold
    }

    /// <summary>
    /// Tests edge cases for rotation parameters
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithEdgeCaseRotations_HandlesCorrectly()
    {
        var imagePath = Path.Combine(TestImagesPath, "generic_guy.png");
        var imageData = await File.ReadAllBytesAsync(imagePath);

        // Test zero rotation limit (should disable rotation)
        var zeroRotationOptions = ProcessingOptions.PivBalanced with
        {
            MaxRotationDegrees = 0.0f,
        };
        var zeroResult = await _encoderService.ProcessAsync(imageData, zeroRotationOptions);
        zeroResult.IsSuccess.Should().BeTrue();
        zeroResult.Value.Metadata.RotationApplied.Should().Be(0.0f);

        // Test very high rotation limit
        var highRotationOptions = ProcessingOptions.PivBalanced with
        {
            MaxRotationDegrees = 45.0f,
        };
        var highResult = await _encoderService.ProcessAsync(imageData, highRotationOptions);
        highResult.IsSuccess.Should().BeTrue();
        highResult.Value.Should().NotBeNull();
    }

    /// <summary>
    /// Tests parameter validation with functional approach
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithInvalidParameters_ReturnsFailure()
    {
        var imagePath = Path.Combine(TestImagesPath, "generic_guy.png");
        var imageData = await File.ReadAllBytesAsync(imagePath);

        // Test with invalid rotation limit (negative)
        var invalidRotationOptions = ProcessingOptions.PivBalanced with
        {
            MaxRotationDegrees = -5.0f,
        };
        var rotationResult = await _encoderService.ProcessAsync(imageData, invalidRotationOptions);
        rotationResult.IsFailure.Should().BeTrue();
        rotationResult.Error.Should().Contain("MaxRotationDegrees");

        // Test with invalid confidence (>1.0)
        var invalidConfidenceOptions = ProcessingOptions.PivBalanced with
        {
            MinFaceConfidence = 1.5f,
        };
        var confidenceResult = await _encoderService.ProcessAsync(
            imageData,
            invalidConfidenceOptions
        );
        confidenceResult.IsFailure.Should().BeTrue();
        confidenceResult.Error.Should().Contain("MinFaceConfidence");
    }

    /// <summary>
    /// Tests timeout handling with functional approach
    /// </summary>
    [Test]
    public async Task ProcessAsync_WithShortTimeout_ReturnsTimeoutFailure()
    {
        var imagePath = Path.Combine(TestImagesPath, "generic_guy.png");
        var imageData = await File.ReadAllBytesAsync(imagePath);

        // Set unrealistically short timeout
        var timeoutOptions = ProcessingOptions.PivBalanced with
        {
            ProcessingTimeout = TimeSpan.FromMilliseconds(1),
        };

        var result = await _encoderService.ProcessAsync(imageData, timeoutOptions);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("timeout");
    }
}
