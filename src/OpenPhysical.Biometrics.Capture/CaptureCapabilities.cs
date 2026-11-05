// SPDX-License-Identifier: GPL-2.0-or-later

using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Capture;

/// <summary>Driver-declared settings; qualification remains separate.</summary>
public sealed record CaptureCapabilities
{
    private CaptureCapabilities(byte min, byte max, bool rolled, bool corrected)
    {
        MinimumExposure = min;
        MaximumExposure = max;
        SupportsRolled = rolled;
        SupportsCorrection = corrected;
    }

    /// <summary>Gets the minimum supported exposure.</summary>
    public byte MinimumExposure { get; }

    /// <summary>Gets the maximum supported exposure.</summary>
    public byte MaximumExposure { get; }

    /// <summary>Gets whether rolled acquisition is available.</summary>
    public bool SupportsRolled { get; }

    /// <summary>Gets whether corrected images are available.</summary>
    public bool SupportsCorrection { get; }

    /// <summary>Creates validated capability bounds.</summary>
    /// <param name="minimumExposure">Minimum exposure.</param>
    /// <param name="maximumExposure">Maximum exposure.</param>
    /// <param name="supportsRolled">Rolled capability.</param>
    /// <param name="supportsCorrection">Correction capability.</param>
    /// <returns>Capabilities or unsupported-capability failure.</returns>
    public static Result<CaptureCapabilities, CaptureError> Create(
        int minimumExposure,
        int maximumExposure,
        bool supportsRolled,
        bool supportsCorrection
    ) =>
        minimumExposure >= 0 && maximumExposure <= 255 && minimumExposure <= maximumExposure
            ? new CaptureCapabilities(
                (byte)minimumExposure,
                (byte)maximumExposure,
                supportsRolled,
                supportsCorrection
            )
            : new CaptureError(
                CaptureErrorCodes.UnsupportedCapability,
                "The exposure range is invalid."
            );

    /// <summary>Checks requested settings against this device.</summary>
    /// <param name="settings">Requested settings.</param>
    /// <returns>Success or unsupported-capability failure.</returns>
    public UnitResult<CaptureError> Validate(CaptureSettings settings) =>
        settings is not null
        && settings.Exposure >= MinimumExposure
        && settings.Exposure <= MaximumExposure
        && (!settings.Rolled || SupportsRolled)
        && (!settings.Corrected || SupportsCorrection)
            ? UnitResult.Success<CaptureError>()
            : UnitResult.Failure(
                new CaptureError(
                    CaptureErrorCodes.UnsupportedCapability,
                    "The device does not support these settings."
                )
            );
}
