// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class CaptureQualityLabelsTests
{
    [AvaloniaTest]
    public async Task ReviewExplainsEachNfiqClassOnItsOwnScale()
    {
        await using var rig = new ProcessingCaptureRig();
        var meanings = new[] { "excellent", "very good", "good", "fair", "poor" };
        for (var index = 0; index < meanings.Length; index++)
        {
            var nfiq = index + 1;
            var capture = StorageFixtures.Capture() with
            {
                Quality = FingerprintQualityAssessment.Create(nfiq, "fixture-nfiq").Value,
            };
            await rig.View.ReviewAcceptedAsync(capture);
            rig.View.Diagnostics.QualityText.Should().Contain($"class {nfiq} ({meanings[index]})");
            rig.View.Status.Should().Contain($"class {nfiq} ({meanings[index]})");
        }
    }

    [AvaloniaTest]
    public async Task UnassessedImagesDoNotClaimAQualityClass()
    {
        await using var rig = new ProcessingCaptureRig();
        var capture = StorageFixtures.Capture() with
        {
            Quality = FingerprintQualityAssessment
                .Create(
                    5,
                    "fixture-nfiq",
                    Maybe<double>.None,
                    FingerprintQualityStatus.InsufficientMinutiae
                )
                .Value,
        };
        await rig.View.ReviewAcceptedAsync(capture);
        rig.View.Diagnostics.QualityText.Should().Be("NFIQ 1 not assessed");
        rig.View.Status.Should().NotContain("(poor)");
    }

    [AvaloniaTest]
    public async Task SupplementalScoreShowsItsIndependentScaleAndDirection()
    {
        await using var rig = new ProcessingCaptureRig();
        foreach (var score in new[] { 0, 100 })
        {
            var capture = StorageFixtures.Capture() with
            {
                Nfiq2 = FingerprintUnifiedQualityAssessment.Create(score, "fixture-nfiq2").Value,
            };
            await rig.View.ReviewAcceptedAsync(capture);
            var diagnostics = rig.View.Diagnostics;
            diagnostics.HasNfiq2.Should().BeTrue();
            diagnostics.Nfiq2Text.Should().Be($"NFIQ 2: {score}/100 (higher is better)");
            rig.View.Status.Should().Contain(diagnostics.Nfiq2Text);
            capture.Quality.PivQuality.Should().Be(80);
        }
    }
}
