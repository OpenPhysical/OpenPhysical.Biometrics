// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

internal static class Fs50FrozenEvidence
{
    internal static UnitResult<CaptureError> Validate<TPolicy>(
        CaptureFrame frame,
        Fs50ContactDiagnostics diagnostics,
        TPolicy policy,
        string mismatchCode,
        CancellationToken cancellationToken
    )
        where TPolicy : class
    {
        if (cancellationToken.IsCancellationRequested)
            return Fs50MaskErrors.Cancelled;
        if (frame is null || diagnostics is null || policy is null)
            return new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Frozen frame, source evidence and policy are required."
            );
        if (
            frame.Width != 800
            || frame.Height != 750
            || frame.Stage != CaptureFrame.FrameStage.Corrected
        )
            return new CaptureError(
                CaptureErrorCodes.UnsupportedGeometry,
                "Post-capture evidence requires the complete corrected FS50 image."
            );
        return frame.PixelDigest == diagnostics.SourceDigest
            ? UnitResult.Success<CaptureError>()
            : new CaptureError(
                mismatchCode,
                "Contact evidence belongs to different source pixels."
            );
    }
}
