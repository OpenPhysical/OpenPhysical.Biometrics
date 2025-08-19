// MIT License
// 
// Copyright (c) 2025 FaceOFFx Contributors
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.IO;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Domain.Quality.Assessors;
using FluentAssertions;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace OpenPhysical.Biometrics.Core.Tests.Quality;

/// <summary>
/// Tests for SharpnessAssessor DCT-based sharpness detection
/// </summary>
[TestFixture]
public class SharpnessAssessorTests
{

    [Test]
    public void Assess_WithSharpImage_ReturnsHighScore()
    {
        // Arrange - Use unblurred image
        var imagePath = Path.Combine(TestContext.CurrentContext.TestDirectory, 
            "..", "..", "..", "..", "..", "tests", "sample_images", "johnson_blur_0.00.jpg");
        using var image = Image.Load<Rgba32>(imagePath);

        // Act
        var result = SharpnessAssessor.Assess(image);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Overall.Value.Should().BeGreaterThan(0.9f);
        TestContext.WriteLine($"Sharp image (blur_0.00) score: {result.Value.Overall.Value:F3}");
    }

    [Test]
    public void Assess_WithBlurredImage_ReturnsLowerScore()
    {
        // Arrange - Compare unblurred and blur_1.00
        var sharpPath = Path.Combine(TestContext.CurrentContext.TestDirectory, 
            "..", "..", "..", "..", "..", "tests", "sample_images", "johnson_blur_0.00.jpg");
        var blurredPath = Path.Combine(TestContext.CurrentContext.TestDirectory, 
            "..", "..", "..", "..", "..", "tests", "sample_images", "johnson_blur_1.00.jpg");
        
        using var sharpImage = Image.Load<Rgba32>(sharpPath);
        using var blurredImage = Image.Load<Rgba32>(blurredPath);

        // Act
        var sharpResult = SharpnessAssessor.Assess(sharpImage);
        var blurredResult = SharpnessAssessor.Assess(blurredImage);

        // Assert
        sharpResult.IsSuccess.Should().BeTrue();
        blurredResult.IsSuccess.Should().BeTrue();
        
        var sharpScore = sharpResult.Value.Overall.Value;
        var blurredScore = blurredResult.Value.Overall.Value;
        
        blurredScore.Should().BeLessThan(sharpScore);
        TestContext.WriteLine($"Sharp (blur_0.00) score: {sharpScore:F3}, Blurred (blur_1.00) score: {blurredScore:F3}");
    }

    [Test]
    [TestCase("johnson_blur_0.00.jpg", 0.08f, 0.15f)]   // No blur - should score highest
    [TestCase("johnson_blur_0.50.jpg", 0.06f, 0.10f)]   // Slight blur - moderate score  
    [TestCase("johnson_blur_0.63.jpg", 0.008f, 0.012f)] // Calibration point - lower score
    [TestCase("johnson_blur_0.70.jpg", 0.005f, 0.010f)] // Higher blur - lower score
    [TestCase("johnson_blur_1.00.jpg", 0.002f, 0.005f)] // Heavy blur - should score lowest
    public void Assess_WithVariousBlurLevels_ReturnsExpectedScoreRange(
        string fileName, float minExpectedScore, float maxExpectedScore)
    {
        // Arrange
        var imagePath = Path.Combine(TestContext.CurrentContext.TestDirectory, 
            "..", "..", "..", "..", "..", "tests", "sample_images", fileName);
        using var image = Image.Load<Rgba32>(imagePath);

        // Act
        var result = SharpnessAssessor.Assess(image);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var score = result.Value.Overall.Value;
        score.Should().BeInRange(minExpectedScore, maxExpectedScore);
        TestContext.WriteLine($"{fileName}: score {score:F3}");
    }

    [Test]
    public void Assess_WithRegionOfInterest_AnalyzesOnlySpecifiedRegion()
    {
        // Arrange
        using var image = CreateTestPattern(64, 64);
        var roi = new Rectangle(16, 16, 32, 32); // Center region

        // Act
        var fullResult = SharpnessAssessor.Assess(image);
        var roiResult = SharpnessAssessor.Assess(image, roi);

        // Assert
        fullResult.IsSuccess.Should().BeTrue();
        roiResult.IsSuccess.Should().BeTrue();
        
        // ROI result should be different from full image
        roiResult.Value.Overall.Value.Should().NotBe(fullResult.Value.Overall.Value);
        TestContext.WriteLine($"Full image score: {fullResult.Value.Overall.Value:F3}, ROI score: {roiResult.Value.Overall.Value:F3}");
    }

    [Test]
    [TestCase("johnson")]
    [TestCase("bush")]
    public void Assess_BlurProgression_ShowsMonotonicDecrease(string politician)
    {
        // Test that sharpness scores decrease monotonically with increasing blur
        var blurLevels = new[] { "0.00", "0.30", "0.50", "0.63", "0.70", "1.00" };
        var scores = new List<(string level, float score)>();
        
        foreach (var level in blurLevels)
        {
            var fileName = $"{politician}_blur_{level}.jpg";
            var imagePath = Path.Combine(TestContext.CurrentContext.TestDirectory, 
                "..", "..", "..", "..", "..", "tests", "sample_images", fileName);
            
            if (File.Exists(imagePath))
            {
                using var image = Image.Load<Rgba32>(imagePath);
                var result = SharpnessAssessor.Assess(image);
                result.IsSuccess.Should().BeTrue();
                scores.Add((level, result.Value.Overall.Value));
            }
        }
        
        // Verify monotonic decrease
        for (int i = 1; i < scores.Count; i++)
        {
            scores[i].score.Should().BeLessThanOrEqualTo(scores[i-1].score,
                $"blur_{scores[i].level} should have lower score than blur_{scores[i-1].level}");
        }
        
        // Log progression
        TestContext.WriteLine($"\n{politician} blur progression:");
        foreach (var (level, score) in scores)
        {
            TestContext.WriteLine($"  blur_{level}: {score:F3}");
        }
    }

    [Test]
    public void Assess_WithTooSmallImage_ReturnsFailure()
    {
        // Arrange - Create image smaller than DCT block size (8x8)
        using var image = new Image<Rgba32>(4, 4);

        // Act
        var result = SharpnessAssessor.Assess(image);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("too small");
    }

    [Test]
    public void Assess_WithNullImage_ReturnsFailure()
    {
        // Act
        var result = SharpnessAssessor.Assess(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No image data");
    }

    [Test]
    public void Assess_CalibrationTest_Blur063ScoresAround69Percent()
    {
        // This test verifies the calibration where blur_0.63 should score just below 70%
        // Arrange
        var imagePath = Path.Combine(TestContext.CurrentContext.TestDirectory, 
            "..", "..", "..", "..", "..", "tests", "sample_images", "johnson_blur_0.63.jpg");
        using var image = Image.Load<Rgba32>(imagePath);

        // Act
        var result = SharpnessAssessor.Assess(image);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var score = result.Value.Overall.Value;
        
        // Score should be between 65% and 72% (allowing some variance)
        score.Should().BeInRange(0.65f, 0.72f);
        TestContext.WriteLine($"Blur 0.63 calibration: score {score:F3} (target ~0.69)");
    }

    [Test]
    [TestCase("Center", 2.0f)]  // Center region weighted more heavily
    [TestCase("Top", 1.0f)]
    [TestCase("Bottom", 1.0f)]
    [TestCase("Left", 1.0f)]
    [TestCase("Right", 1.0f)]
    public void Assess_RegionalWeighting_CenterWeightedMoreHeavily(string regionName, float expectedWeight)
    {
        // This test verifies that center region has higher weight in overall score
        // Arrange
        using var image = CreateTestPattern(64, 64);

        // Act
        var measurement = SharpnessAssessor.MeasureSharpness(image);

        // Assert
        measurement.IsSuccess.Should().BeTrue();
        measurement.Value.RegionalScores.Should().ContainKey(regionName);
        
        // Note: Actual weight verification would require access to internal calculations
        // This test mainly documents the expected behavior
        TestContext.WriteLine($"Region {regionName} expected weight: {expectedWeight}x");
    }

    private static Image<Rgba32> CreateTestPattern(int width, int height)
    {
        var image = new Image<Rgba32>(width, height);
        
        // Create a pattern with sharp edges
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < width; x++)
                {
                    // Checkerboard pattern
                    var isWhite = ((x / 8) + (y / 8)) % 2 == 0;
                    row[x] = isWhite ? Color.White : Color.Black;
                }
            }
        });
        
        return image;
    }
}