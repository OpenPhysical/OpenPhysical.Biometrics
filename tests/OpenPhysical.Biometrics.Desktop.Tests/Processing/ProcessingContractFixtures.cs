// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

internal static class ProcessingContractFixtures
{
    internal static CaptureProcessingRequest Request(
        int brightness = 0,
        long revision = 1,
        bool whiten = false,
        int margin = 32,
        int guard = 8
    ) =>
        new(
            FingerprintPreprocessingPolicy
                .Create(claheEnabled: whiten, blend: 0, brightness: brightness, contrast: 0)
                .Value,
            Fs50EvidencePolicy.Default,
            Fs50ContactContourPolicy
                .Create(
                    new Fs50ContactContourOptions
                    {
                        ContextMargin = margin,
                        ModifiedBoundaryGuard = guard,
                    }
                )
                .Value,
            revision,
            CaptureProcessingInput.DeclaredOfflineImage
        );

    internal static CapturePreparedFrame Prepared(
        ScoringTestServices services,
        CaptureFrame frame,
        int brightness = 0,
        long revision = 1
    ) =>
        services
            .Processor.PrepareFrame(frame, Request(brightness, revision), CancellationToken.None)
            .Value;

    internal static Result<CapturedFingerprint, DesktopError> Capture(
        ICaptureProcessor processor,
        CaptureFrame frame,
        FingerPosition finger
    ) =>
        processor
            .EvaluateFrame(frame, Request(), CancellationToken.None)
            .Bind(selection =>
                processor.Extract(selection, finger, default, CancellationToken.None)
            );
}
