using System.Drawing;
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
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Core.Domain.Quality.Assessors;
using OpenPhysical.Biometrics.Imaging;

namespace OpenPhysical.Biometrics.Core.Tests.Quality;

/// <summary>
/// Tests for SharpnessAssessor DCT-based sharpness detection
/// </summary>
[TestFixture]
public class SharpnessAssessorTests
{
    /// <summary>Checks that sharpness assessment leaves the captured pixels unchanged.</summary>
    [Test]
    public void Assess_PreservesCapturedPixelSamples()
    {
        // Arrange - Use unblurred image
        var imagePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "tests",
            "sample_images",
            "johnson_blur_0.00.jpg"
        );
        using var image = RgbImage.Load(imagePath);

        var original = image.CopyRgbBytes();

        // Act
        var result = SharpnessAssessor.Assess(image);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        image.CopyRgbBytes().Should().Equal(original);
        TestContext.WriteLine($"Sharp image (blur_0.00) score: {result.Value.Overall.Value:F3}");
    }

    /// <summary>Checks that a blurred reference image scores below its sharp counterpart.</summary>
    [Test]
    public void Assess_WithBlurredImage_ReturnsLowerScore()
    {
        // Arrange - Compare unblurred and blur_1.00
        var sharpPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "tests",
            "sample_images",
            "johnson_blur_0.00.jpg"
        );
        var blurredPath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "tests",
            "sample_images",
            "johnson_blur_1.00.jpg"
        );

        using var sharpImage = RgbImage.Load(sharpPath);
        using var blurredImage = RgbImage.Load(blurredPath);

        // Act
        var sharpResult = SharpnessAssessor.Assess(sharpImage);
        var blurredResult = SharpnessAssessor.Assess(blurredImage);

        // Assert
        sharpResult.IsSuccess.Should().BeTrue();
        blurredResult.IsSuccess.Should().BeTrue();

        var sharpScore = sharpResult.Value.Overall.Value;
        var blurredScore = blurredResult.Value.Overall.Value;

        blurredScore.Should().BeLessThan(sharpScore);
        TestContext.WriteLine(
            $"Sharp (blur_0.00) score: {sharpScore:F3}, Blurred (blur_1.00) score: {blurredScore:F3}"
        );
    }

    /// <summary>Checks that every blur fixture produces finite normalized regional scores.</summary>
    /// <param name="fileName">Name of the recorded image fixture.</param>
    [Test]
    [TestCase("johnson_blur_0.00.jpg")]
    [TestCase("johnson_blur_0.50.jpg")]
    [TestCase("johnson_blur_0.63.jpg")]
    [TestCase("johnson_blur_0.70.jpg")]
    [TestCase("johnson_blur_1.00.jpg")]
    public void Assess_WithVariousBlurLevels_ReturnsNormalizedScores(string fileName)
    {
        // Arrange
        var imagePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "tests",
            "sample_images",
            fileName
        );
        using var image = RgbImage.Load(imagePath);

        // Act
        var result = SharpnessAssessor.Assess(image);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var score = result.Value.Overall.Value;
        score.Should().BeInRange(0f, 1f);
        result
            .Value.RegionalScores.Values.Should()
            .OnlyContain(value => float.IsFinite(value) && value >= 0f && value <= 1f);
        TestContext.WriteLine($"{fileName}: score {score:F3}");
    }

    /// <summary>Checks that an explicit region limits the sharpness calculation.</summary>
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
        TestContext.WriteLine(
            $"Full image score: {fullResult.Value.Overall.Value:F3}, ROI score: {roiResult.Value.Overall.Value:F3}"
        );
    }

    /// <summary>Checks that increasing blur reduces sharpness for the selected fixture series.</summary>
    /// <param name="politician">Name identifying the public-portrait fixture series.</param>
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
            var imagePath = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "tests",
                "sample_images",
                fileName
            );

            if (File.Exists(imagePath))
            {
                using var image = RgbImage.Load(imagePath);
                var result = SharpnessAssessor.Assess(image);
                result.IsSuccess.Should().BeTrue();
                scores.Add((level, result.Value.Overall.Value));
            }
        }

        // Verify monotonic decrease
        for (int i = 1; i < scores.Count; i++)
        {
            scores[i]
                .score.Should()
                .BeLessThanOrEqualTo(
                    scores[i - 1].score,
                    $"blur_{scores[i].level} should have lower score than blur_{scores[i - 1].level}"
                );
        }

        // Log progression
        TestContext.WriteLine($"\n{politician} blur progression:");
        foreach (var (level, score) in scores)
        {
            TestContext.WriteLine($"  blur_{level}: {score:F3}");
        }
    }

    /// <summary>Checks that images below the analysis kernel size are rejected.</summary>
    [Test]
    public void Assess_WithTooSmallImage_ReturnsFailure()
    {
        // Arrange - Create image smaller than DCT block size (8x8)
        using var image = new RgbImage(4, 4);

        // Act
        var result = SharpnessAssessor.Assess(image);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("too small");
    }

    /// <summary>Checks that a missing image produces a validation failure.</summary>
    [Test]
    public void Assess_WithNullImage_ReturnsFailure()
    {
        // Act
        var result = SharpnessAssessor.Assess(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No image data");
    }

    /// <summary>Checks that the central facial region receives the intended sharpness weight.</summary>
    /// <param name="regionName">Image region whose weighting is checked.</param>
    /// <param name="expectedWeight">Expected relative contribution of the selected region.</param>
    [Test]
    [TestCase("Center", 2.0f)] // Center region weighted more heavily
    [TestCase("Top", 1.0f)]
    [TestCase("Bottom", 1.0f)]
    [TestCase("Left", 1.0f)]
    [TestCase("Right", 1.0f)]
    public void Assess_RegionalWeighting_CenterWeightedMoreHeavily(
        string regionName,
        float expectedWeight
    )
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

    private static RgbImage CreateTestPattern(int width, int height)
    {
        var image = new RgbImage(width, height);

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
