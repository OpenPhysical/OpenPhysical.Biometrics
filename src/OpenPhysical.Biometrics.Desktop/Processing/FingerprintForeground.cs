// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.NBIS;

namespace OpenPhysical.Biometrics.Desktop;

internal static class FingerprintForeground
{
    internal static Result<Fs50ContactRegion, DesktopError> Analyze(CaptureFrame frame) =>
        AnalyzeCore(frame, () => Fs50ContactAnalysis.Analyze(frame.Pixels.AsSpan()));

    internal static Result<Fs50ContactRegion, DesktopError> Analyze(
        CaptureFrame frame,
        Fs50EvidencePolicy policy
    ) => AnalyzeCore(frame, () => Fs50ContactAnalysis.Analyze(frame.Pixels.AsSpan(), policy));

    private static Result<Fs50ContactRegion, DesktopError> AnalyzeCore(
        CaptureFrame frame,
        Func<Result<Fs50ContactRegion, CaptureError>> analyze
    )
    {
        if (frame.Width != 800 || frame.Height != 750)
            return new DesktopError(
                "Capture.Geometry",
                "The FS50 image must be 800 by 750 pixels."
            );
        return analyze()
            .MapError(error => new DesktopError(error.Code, error.Message))
            .Check(region => CaptureContactFeedback.Validate(region.Diagnostics));
    }

    internal static Result<FingerprintTemplate, DesktopError> Select(
        FingerprintTemplate template,
        System.Collections.Immutable.ImmutableArray<byte> mask
    ) =>
        OpenPhysical
            .Biometrics.Fingerprints.FingerprintRegionSelection.Select(template, mask)
            .MapError(error => new DesktopError(error.Code, error.Message))
            .Ensure(
                selected => selected.Minutiae.Length > 0,
                new DesktopError(
                    "Capture.NoFeatures",
                    "No ridge features were found inside the finger region. Try another capture."
                )
            );
}
