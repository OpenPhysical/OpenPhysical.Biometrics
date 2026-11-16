// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

/// <summary>Stable webcam acquisition failures.</summary>
public static class CameraErrorCodes
{
    /// <summary>The required native camera runtime is not bundled or cannot be loaded.</summary>
    public const string NativeDependency = "camera.NativeDependency";

    /// <summary>The requested native RGB mode is unavailable.</summary>
    public const string UnsupportedMode = "camera.UnsupportedMode";

    /// <summary>The camera cannot be enumerated or opened.</summary>
    public const string Unavailable = "camera.Unavailable";

    /// <summary>The camera stopped delivering frames within the acquisition deadline.</summary>
    public const string FrameTimeout = "camera.FrameTimeout";

    /// <summary>The supplied frame bytes do not match the native mode.</summary>
    public const string InvalidFrame = "camera.InvalidFrame";

    /// <summary>The operation was cancelled.</summary>
    public const string Cancelled = "camera.Cancelled";

    /// <summary>The camera session has been stopped.</summary>
    public const string Closed = "camera.Closed";

    /// <summary>Camera permission was denied by the operating system.</summary>
    public const string PermissionDenied = "camera.PermissionDenied";
}
