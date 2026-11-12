// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

[TestFixture]
internal sealed class CaptureContourSourceTests
{
    [Test]
    public void SamePolicyDiagnosticsFromAnotherSourceAreRejectedBeforeNativeCalls()
    {
        using var services = new ScoringTestServices();
        var pixels = ProcessingFrameFixtures.PublicFinger();
        var frame = ProcessingFrameFixtures.NormalizedFrame(pixels);
        var policy = Fs50ContactContourPolicy.Create(new Fs50ContactContourOptions()).Value;
        var prepared = services
            .Processor.PrepareFrame(
                frame,
                new(
                    Original(),
                    Fs50EvidencePolicy.Default,
                    policy,
                    17,
                    CaptureProcessingInput.DeclaredOfflineImage
                ),
                CancellationToken.None
            )
            .Value;
        pixels[0] = 254;
        var wrong = Fs50ContactAnalysis.Analyze(pixels).Value.Diagnostics;
        var result = services.Processor.EvaluateArtifact(
            prepared with
            {
                Diagnostics = wrong,
            },
            OpenPhysical.Biometrics.Fingerprints.FingerprintQualitySelectionPolicy.Default,
            CancellationToken.None
        );
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Capture.ContourSourceMismatch");
        services.Counts.Should().Be((0, 0, 0));
    }

    private static FingerprintPreprocessingPolicy Original() =>
        FingerprintPreprocessingPolicy
            .Create(claheEnabled: false, brightness: 0, contrast: 0)
            .Value;
}
