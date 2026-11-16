// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class PortraitBackgroundTests
{
    [Test]
    public void FrozenBackgroundReplacementPreservesFacePixelsAndOriginalSource()
    {
        using var service = TestFixtures.NewPreparer();
        var analysis = service
            .Analyze(PortraitImage.Decode(TestFixtures.Bytes("bush.jpg")).Value)
            .Value;
        var original = service.Prepare(analysis).Value;
        var enriched = service.PrepareBackground(analysis).Value;
        var result = service.Prepare(
            enriched,
            default,
            default,
            PortraitMouthPolicy.Default,
            PortraitBackgroundPolicy.Default
        );
        result.Should().Succeed();
        var portrait = result.Value;
        portrait.SourceSha256.Should().Be(original.SourceSha256);
        portrait.Transform.Value.Should().Be(original.Transform.Value);
        portrait.Findings.Should().Equal(original.Findings);
        AssertFaceUnchanged(original, portrait);
        portrait.Background.Value.ReplacedPixelCount.Should().BeGreaterThan(1000);
        portrait
            .Background.Value.OriginalPortraitSha256.Should()
            .NotBe(portrait.Background.Value.PortraitSha256);
        var fill = portrait.Background.Value.Fill.Value;
        portrait.RgbPixels.Take(3).Should().Equal(fill.Red, fill.Green, fill.Blue);
        SaveReview(original, portrait);
        analysis.PersonMask.HasNoValue.Should().BeTrue();
        service
            .PrepareBackground(enriched)
            .Value.PersonMask.Value.Should()
            .BeSameAs(enriched.PersonMask.Value);
    }

    [Test]
    public void DisablingReplacementRestoresTheExactOriginalCropWithoutAnotherSegmentation()
    {
        using var service = TestFixtures.NewPreparer();
        var analysis = service
            .Analyze(PortraitImage.Decode(TestFixtures.Bytes("bush.jpg")).Value)
            .Value;
        var original = service.Prepare(analysis).Value;
        var enriched = service.PrepareBackground(analysis).Value;
        var restored = service
            .Prepare(
                enriched,
                default,
                default,
                PortraitMouthPolicy.Default,
                PortraitBackgroundPolicy.Default with
                {
                    Enabled = false,
                }
            )
            .Value;
        restored.RgbPixels.Should().Equal(original.RgbPixels);
        restored.Background.HasNoValue.Should().BeTrue();
    }

    private static bool SaveReview(
        PortraitPreparationResult original,
        PortraitPreparationResult processed
    )
    {
        var folder = Path.Combine(Path.GetTempPath(), "openphysical-background-public");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(
            Path.Combine(folder, "original.png"),
            PortraitEncoding
                .EncodePng(original.RgbPixels.AsSpan(), PortraitOutput.Width, PortraitOutput.Height)
                .Value
        );
        File.WriteAllBytes(
            Path.Combine(folder, "processed.png"),
            PortraitEncoding
                .EncodePng(
                    processed.RgbPixels.AsSpan(),
                    PortraitOutput.Width,
                    PortraitOutput.Height
                )
                .Value
        );
        return true;
    }

    private static bool AssertFaceUnchanged(
        PortraitPreparationResult original,
        PortraitPreparationResult composite
    )
    {
        for (var y = 300; y < 330; y++)
        for (var x = 225; x < 255; x++)
        for (var c = 0; c < 3; c++)
            composite
                .RgbPixels[(y * PortraitOutput.Width + x) * 3 + c]
                .Should()
                .Be(original.RgbPixels[(y * PortraitOutput.Width + x) * 3 + c]);
        return true;
    }
}
