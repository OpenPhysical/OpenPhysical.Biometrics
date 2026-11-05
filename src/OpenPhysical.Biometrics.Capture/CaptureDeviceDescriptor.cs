// SPDX-License-Identifier: GPL-2.0-or-later

using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Capture;

/// <summary>An immutable device identity without person or credential data.</summary>
public sealed record CaptureDeviceDescriptor
{
    private CaptureDeviceDescriptor(
        string id,
        string driverId,
        string model,
        Maybe<string> serial,
        CaptureCapabilities capabilities
    )
    {
        DeviceId = id;
        DriverId = driverId;
        Model = model;
        Serial = serial;
        Capabilities = capabilities;
    }

    /// <summary>Gets a canonical physical identity shared across drivers and registry instances.</summary>
    public string DeviceId { get; }

    /// <summary>Gets the driver registration identifier.</summary>
    public string DriverId { get; }

    /// <summary>Gets the reported model.</summary>
    public string Model { get; }

    /// <summary>Gets the serial number when the device reports one.</summary>
    public Maybe<string> Serial { get; }

    /// <summary>Gets declared capabilities.</summary>
    public CaptureCapabilities Capabilities { get; }

    /// <summary>Validates bounded device identity metadata.</summary>
    /// <param name="deviceId">Canonical physical-device identity.</param>
    /// <param name="driverId">Driver identifier.</param>
    /// <param name="model">Device model.</param>
    /// <param name="serial">Optional serial number.</param>
    /// <param name="capabilities">Declared capabilities.</param>
    /// <returns>A descriptor or invalid-argument failure.</returns>
    public static Result<CaptureDeviceDescriptor, CaptureError> Create(
        string deviceId,
        string driverId,
        string model,
        Maybe<string> serial,
        CaptureCapabilities capabilities
    ) =>
        ValidText(deviceId)
        && ValidText(driverId)
        && ValidText(model)
        && capabilities is not null
        && (!serial.HasValue || ValidText(serial.Value))
            ? new CaptureDeviceDescriptor(deviceId, driverId, model, serial, capabilities)
            : new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Device identity metadata is invalid."
            );

    internal static bool ValidText(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 256 && !value.Any(char.IsControl);
}
