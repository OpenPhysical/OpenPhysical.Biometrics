// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture;

/// <summary>Stable acquisition failure codes.</summary>
public static class CaptureErrorCodes
{
    /// <summary>The device cannot be reached.</summary>
    public const string DeviceUnavailable = "capture.DeviceUnavailable";

    /// <summary>Another acquisition owns the device.</summary>
    public const string DeviceBusy = "capture.DeviceBusy";

    /// <summary>The requested setting is not supported.</summary>
    public const string UnsupportedCapability = "capture.UnsupportedCapability";

    /// <summary>Image dimensions are invalid or unsupported.</summary>
    public const string UnsupportedGeometry = "capture.UnsupportedGeometry";

    /// <summary>Correction or provenance metadata is inconsistent.</summary>
    public const string CalibrationInvalid = "capture.CalibrationInvalid";

    /// <summary>The image byte count is incomplete.</summary>
    public const string TransferIncomplete = "capture.TransferIncomplete";

    /// <summary>A bounded transfer timed out without transferring any bytes.</summary>
    public const string TransferTimedOut = "capture.TransferTimedOut";

    /// <summary>The scanner endpoint could not be synchronized within its cleanup budget.</summary>
    public const string SynchronizationFailed = "capture.SynchronizationFailed";

    /// <summary>Acquisition was cancelled.</summary>
    public const string Cancelled = "capture.Cancelled";

    /// <summary>A required argument is invalid.</summary>
    public const string InvalidArgument = "capture.InvalidArgument";

    /// <summary>A driver returned invalid session evidence.</summary>
    public const string InvalidFrame = "capture.InvalidFrame";
}
