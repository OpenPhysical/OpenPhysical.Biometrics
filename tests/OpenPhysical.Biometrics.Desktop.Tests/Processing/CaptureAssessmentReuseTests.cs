// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class CaptureAssessmentReuseTests
{
    [TestCase(0, false, 1)]
    [TestCase(0, true, 1)]
    [TestCase(5, false, 2)]
    public void OnlyExactOriginalPixelsReuseTheOwningEngineAssessment(
        int brightness,
        bool zeroBlendEnabled,
        int expected
    )
    {
        using var services = new ScoringTestServices();
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        var selection = services
            .Processor.EvaluateFrame(
                frame,
                Request(brightness, zeroBlendEnabled),
                CancellationToken.None
            )
            .Value;
        var capture = services
            .Processor.Extract(
                selection,
                FingerPosition.RightIndex,
                default,
                CancellationToken.None
            )
            .Value;
        if (brightness == 0)
        {
            selection.SelectedArtifact.Pixels.Should().Equal(frame.Pixels);
            selection.Selected.ProcessingRegion.AppliedGuard.Should().Be(0);
            capture
                .Processing.Value.OriginalAssessment.Nfiq2.Value.Should()
                .BeSameAs(selection.Selected.Processed.Evidence.Nfiq2.Value);
        }
        services.Counts.Should().Be((expected, expected, 1));
        capture.Processing.Value.OriginalAssessment.Role.Should().Be(CaptureImageRole.Original);
        capture
            .Processing.Value.OriginalAssessment.ImageDigest.Should()
            .Be(selection.SelectedArtifact.OriginalDigest);
    }

    [Test]
    public void ForeignEngineOwnershipRequiresItsOwnOriginalAssessment()
    {
        using var first = new ScoringTestServices();
        using var second = new ScoringTestServices();
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        var selection = first
            .Processor.EvaluateFrame(
                frame,
                ProcessingContractFixtures.Request(),
                CancellationToken.None
            )
            .Value;
        var result = second.Processor.Extract(
            selection,
            FingerPosition.RightIndex,
            default,
            CancellationToken.None
        );
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : string.Empty);
        first.Counts.Should().Be((1, 1, 0));
        second.Counts.Should().Be((1, 1, 1));
    }

    [Test]
    public void CurrentDefaultAssessesItsChangedPixelsAndOriginalSeparately()
    {
        using var services = new ScoringTestServices();
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        var request = ProcessingContractFixtures.Request() with
        {
            Preprocessing = CapturePolicySettings.Default.Enhancement.For(
                ImageAdjustmentSettings.Default
            ),
        };
        var selected = services.Processor.EvaluateFrame(frame, request, default).Value;
        var capture = services.Processor.Extract(
            selected,
            FingerPosition.RightIndex,
            default,
            default
        );
        capture.IsSuccess.Should().BeTrue();
        selected.SelectedArtifact.Pixels.Should().NotEqual(frame.Pixels);
        services.Engine.LastPixels.Should().Equal(selected.SelectedArtifact.Pixels);
        services.Counts.Should().Be((2, 2, 1));
    }

    private static CaptureProcessingRequest Request(int brightness, bool zeroBlendEnabled) =>
        ProcessingContractFixtures.Request(brightness) with
        {
            Preprocessing = FingerprintPreprocessingPolicy
                .Create(
                    claheEnabled: zeroBlendEnabled,
                    blend: 0,
                    brightness: brightness,
                    contrast: 0
                )
                .Value,
        };
}
