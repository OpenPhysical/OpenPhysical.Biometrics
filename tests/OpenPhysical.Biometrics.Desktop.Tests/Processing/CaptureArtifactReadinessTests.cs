// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class CaptureArtifactReadinessTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void RejectedOriginalOrContrastSourceCannotReachEitherQualityEngine(bool original)
    {
        using var services = new ScoringTestServices();
        var pixels = ProcessingFrameFixtures
            .PublicFinger()
            .Select(value => (byte)(235 + value * 20 / 255))
            .ToArray();
        var result = services.Processor.EvaluateFrame(
            ProcessingFrameFixtures.NormalizedFrame(pixels),
            ProcessingContractFixtures.Request() with
            {
                Preprocessing = CapturePolicySettings.Default.Enhancement.For(
                    original ? ImageAdjustmentSettings.Original : ImageAdjustmentSettings.Default
                ),
            },
            CancellationToken.None
        );
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Capture.ContactTooLight");
        services.Counts.Should().Be((0, 0, 0));
    }

    [Test]
    public void ReplacedEmptyMaskCannotBypassSourceAssociation()
    {
        using var services = new ScoringTestServices();
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        var prepared = ProcessingContractFixtures.Prepared(services, frame);
        var empty = new byte[frame.Pixels.Length];
        var mask = FingerprintProcessingMask.Create(frame.Width, frame.Height, empty, empty).Value;
        var artifact = FingerprintPreprocessing
            .Process(frame, mask, prepared.Artifact.Policy, CancellationToken.None)
            .Value;
        var result = services.Processor.EvaluateArtifact(
            prepared with
            {
                Artifact = artifact,
            },
            FingerprintQualitySelectionPolicy.Default,
            CancellationToken.None
        );
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Capture.ContourSourceMismatch");
        services.Counts.Should().Be((0, 0, 0));
    }

    [Test]
    public void ASecondPaleContactCannotReachNativeProcessingBesideOneUsableFinger()
    {
        using var services = new ScoringTestServices();
        var pixels = ProcessingFrameFixtures.PublicFinger();
        for (var y = 200; y < 360; y++)
        for (var x = 32; x < 112; x++)
            pixels[(y * 800) + x] = x % 8 < 4 ? (byte)225 : (byte)245;
        var contact = Fs50ContactAnalysis.Analyze(pixels).Value;
        contact.Issue.Should().Be(Fs50ContactIssue.None);
        contact.ComponentCount.Should().Be(1);
        contact.SpatialComponentCount.Should().Be(2);
        services
            .Processor.EvaluateFrame(
                ProcessingFrameFixtures.NormalizedFrame(pixels),
                ProcessingContractFixtures.Request(),
                CancellationToken.None
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Capture.MultipleFingers");
        services.Counts.Should().Be((0, 0, 0));
    }
}
