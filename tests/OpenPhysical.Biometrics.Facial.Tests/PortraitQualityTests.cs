// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Imaging;

internal sealed class PortraitQualityTests
{
    [Test]
    public void AdvisoryMeasurementsAreDeterministicSourceBoundAndDoNotChangePortraitPixels()
    {
        var (portrait, review) = TestFixtures.PreparedPortrait();
        portrait.Status.Should().Be(PortraitStatus.ReviewRequired);
        portrait.Findings.Should().Contain(value => value.Code == "MouthOpen");
        var before = portrait.RgbPixels.ToArray();
        var measured = PortraitQualityAssessor.Assess(portrait, review);
        measured.Should().Succeed();
        measured.Value.SourceSha256.Should().Be(portrait.SourceSha256);
        measured.Value.MaskedLaplacianVariance.Value.Should().BeGreaterThan(0);
        PortraitQualityAssessor
            .Assess(portrait, review)
            .Value.Should()
            .BeEquivalentTo(measured.Value);
        portrait.RgbPixels.Should().Equal(before);
        PortraitQualityAssessor
            .Assess(portrait, review with { SourceSha256 = "wrong" })
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FacialErrorCodes.GeometryMismatch);
        var flat = portrait with { RgbPixels = [.. Enumerable.Repeat((byte)128, before.Length)] };
        var insufficient = PortraitQualityAssessor.Assess(flat, review).Value;
        insufficient.MaskedLaplacianVariance.HasValue.Should().BeFalse();
        insufficient.Findings.Should().Contain(value => value.Code == "SharpnessNotAssessable");
    }

    [Test]
    public void BlurReducesRawSharpnessAndBackdropRequiresExplicitProtectedHeadBounds()
    {
        var (portrait, review) = TestFixtures.PreparedPortrait();
        var baseline = PortraitQualityAssessor.Assess(portrait, review).Value;
        baseline.Backdrop.EstimatedCandidate.Should().BeFalse();
        using var pixels = RgbImageBytes.FromRgbBytes(portrait.RgbPixels.AsSpan(), 480, 640).Value;
        pixels.GaussianBlur(3).Should().Succeed();
        var blurred = portrait with { RgbPixels = [.. pixels.CopyRgbBytes().Value] };
        PortraitQualityAssessor
            .Assess(blurred, review)
            .Value.MaskedLaplacianVariance.Value.Should()
            .BeLessThan(baseline.MaskedLaplacianVariance.Value);
        var flat = portrait with { RgbPixels = [.. Enumerable.Repeat((byte)128, 480 * 640 * 3)] };
        var protectedHead = new PortraitForegroundProtection(40, 40, 440, 590, 590);
        var background = PortraitQualityAssessor.Assess(flat, review, protectedHead).Value.Backdrop;
        background.EstimatedCandidate.Should().BeTrue();
        background.Statistics.Red.Value.Mean.Should().Be(128);
        background.ProtectedForeground.PixelCount.Should().BeGreaterThan(0);
        var dirty = flat.RgbPixels.ToArray();
        for (var y = 0; y < 12; y++)
        for (var x = 0; x < 12; x++)
        for (var c = 0; c < 3; c++)
            dirty[(y * 480 + x) * 3 + c] = (byte)((x + y) % 2 * 255);
        var rejected = PortraitQualityAssessor
            .Assess(flat with { RgbPixels = [.. dirty] }, review, protectedHead)
            .Value.Backdrop;
        rejected.Unclassified.PixelCount.Should().BeGreaterThan(0);
        rejected.Statistics.Red.Value.Mean.Should().Be(128);
    }
}
