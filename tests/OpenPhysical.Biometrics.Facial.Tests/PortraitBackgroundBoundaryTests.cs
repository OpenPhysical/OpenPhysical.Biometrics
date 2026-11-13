// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class PortraitBackgroundBoundaryTests
{
    [Test]
    public void CapturedBackgroundHasAGradualBoundaryInsteadOfOnlyAHalfOpacityStep()
    {
        using var service = TestFixtures.NewPreparer();
        var analysis = service
            .PrepareBackground(
                service.Analyze(PortraitImage.Decode(TestFixtures.Bytes("bush.jpg")).Value).Value
            )
            .Value;
        var original = service.Prepare(analysis).Value;
        var result = service
            .Prepare(
                analysis,
                default,
                default,
                PortraitMouthPolicy.Default,
                PortraitBackgroundPolicy.Default
            )
            .Value;
        var transitions = Transitions(original, result, result.Background.Value.Fill.Value);
        transitions.SofterBackground.Should().BeGreaterThan(20);
        transitions.SofterForeground.Should().BeGreaterThan(20);
        var folder = Path.Combine(Path.GetTempPath(), "openphysical-background-public");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(
            Path.Combine(folder, "after-defringe.png"),
            PortraitEncoding
                .EncodePng(result.RgbPixels.AsSpan(), PortraitOutput.Width, PortraitOutput.Height)
                .Value
        );
    }

    private static (int SofterBackground, int SofterForeground) Transitions(
        PortraitPreparationResult original,
        PortraitPreparationResult result,
        PortraitBackgroundColor fill
    )
    {
        var colors = new[] { fill.Red, fill.Green, fill.Blue };
        var low = 0;
        var high = 0;
        for (var i = 0; i < original.RgbPixels.Length; i += 3)
        {
            var channel = Enumerable
                .Range(0, 3)
                .MaxBy(c => Math.Abs(original.RgbPixels[i + c] - colors[c]));
            var difference = original.RgbPixels[i + channel] - colors[channel];
            if (Math.Abs(difference) < 64)
                continue;
            var alpha = (double)(result.RgbPixels[i + channel] - colors[channel]) / difference;
            if (alpha is > .05 and < .3)
                low++;
            if (alpha is > .7 and < .95)
                high++;
        }
        return (low, high);
    }
}
