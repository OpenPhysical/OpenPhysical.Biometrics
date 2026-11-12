// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

internal static class Fs50MaskErrors
{
    internal static CaptureError Cancelled =>
        new(CaptureErrorCodes.Cancelled, "Post-capture mask preparation was cancelled.");
}
