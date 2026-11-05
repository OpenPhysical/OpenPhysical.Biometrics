// SPDX-License-Identifier: GPL-2.0-or-later

using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Capture;

/// <summary>Requested acquisition settings with an explicit finite session lifetime.</summary>
public sealed record CaptureSettings
{
    private CaptureSettings(byte exposure, bool rolled, bool corrected, TimeSpan lifetime)
    {
        Exposure = exposure;
        Rolled = rolled;
        Corrected = corrected;
        Lifetime = lifetime;
    }

    /// <summary>Gets exposure in the device-independent byte range.</summary>
    public byte Exposure { get; }

    /// <summary>Gets whether a rolled acquisition is requested.</summary>
    public bool Rolled { get; }

    /// <summary>Gets whether calibrated corrected frames are requested.</summary>
    public bool Corrected { get; }

    /// <summary>Gets the total lifetime, including opening the device.</summary>
    public TimeSpan Lifetime { get; }

    /// <summary>Creates bounded settings.</summary>
    /// <param name="exposure">Requested exposure, zero through 255.</param>
    /// <param name="rolled">Whether acquisition is rolled.</param>
    /// <param name="corrected">Whether corrected frames are required.</param>
    /// <param name="lifetime">Positive session lifetime, at most five minutes.</param>
    /// <returns>Settings or unsupported-capability failure.</returns>
    public static Result<CaptureSettings, CaptureError> Create(
        int exposure,
        bool rolled,
        bool corrected,
        TimeSpan lifetime
    ) =>
        exposure is >= 0 and <= 255
        && lifetime > TimeSpan.Zero
        && lifetime <= TimeSpan.FromMinutes(5)
            ? new CaptureSettings((byte)exposure, rolled, corrected, lifetime)
            : new CaptureError(
                CaptureErrorCodes.UnsupportedCapability,
                "Exposure or session lifetime is outside supported bounds."
            );
}
