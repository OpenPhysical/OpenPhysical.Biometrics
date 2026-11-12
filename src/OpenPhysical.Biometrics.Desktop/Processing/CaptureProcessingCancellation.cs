// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

internal static class CaptureProcessingCancellation
{
    internal static UnitResult<DesktopError> Check(CancellationToken token) =>
        token.IsCancellationRequested
            ? UnitResult.Failure(
                new DesktopError(
                    CaptureErrorCodes.Cancelled,
                    "Fingerprint processing was cancelled."
                )
            )
            : UnitResult.Success<DesktopError>();
}
