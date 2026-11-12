// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Usb;

using CSharpFunctionalExtensions;

internal static class LibUsbTransferResult
{
    internal static Result<int, CaptureError> Decode(
        int status,
        int count,
        int capacity,
        bool input
    )
    {
        if (count < 0 || count > capacity)
            return Unavailable();
        // A timed-out input can still contain bytes; only a zero-byte timeout proves quiet.
        if (status == 0 || status == -7 && count > 0 && input)
            return count;
        if (status == -7)
            return count == 0
                ? new CaptureError(
                    CaptureErrorCodes.TransferTimedOut,
                    "USB transfer timed out without data."
                )
                : new CaptureError(
                    CaptureErrorCodes.TransferIncomplete,
                    "USB command timed out after a partial transfer."
                );
        return Unavailable();
    }

    private static CaptureError Unavailable() =>
        new(CaptureErrorCodes.DeviceUnavailable, "USB transfer failed.");
}
