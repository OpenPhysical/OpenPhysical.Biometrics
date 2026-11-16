// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using CSharpFunctionalExtensions;

internal static class AvFoundationStatus
{
    internal static UnitResult<CameraError> Check(int status) =>
        status == 0 ? UnitResult.Success<CameraError>() : Error(status);

    internal static CameraError Error(int status) =>
        status switch
        {
            1 => new(
                CameraErrorCodes.PermissionDenied,
                "Allow Studio camera access in macOS privacy settings."
            ),
            2 => new(
                CameraErrorCodes.Unavailable,
                "The selected camera is unavailable or disconnected."
            ),
            3 => new(
                CameraErrorCodes.UnsupportedMode,
                "This camera cannot deliver the selected native mode in sRGB."
            ),
            4 => new(
                CameraErrorCodes.InvalidFrame,
                "The camera changed its image geometry or pixel format. Choose a supported mode and reconnect."
            ),
            6 => new(CameraErrorCodes.Closed, "The camera session is closed."),
            _ => new(
                CameraErrorCodes.Unavailable,
                "The native camera operation failed. Reconnect the camera."
            ),
        };
}
