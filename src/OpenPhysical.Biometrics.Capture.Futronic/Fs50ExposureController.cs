// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Explicit exposure recommendations; never mutates an acquired image or session.</summary>
public static class Fs50ExposureController
{
    /// <summary>Proposes a bounded change only for substantial contact, unless locked.</summary>
    /// <param name="exposure">Current exposure, including manual zero and 255.</param>
    /// <param name="contact">Validated contact statistics from the current corrected frame.</param>
    /// <param name="enabled">Whether automatic exposure was explicitly enabled.</param>
    /// <param name="locked">Whether exposure is locked, including during a roll.</param>
    /// <returns>The next exposure, at most 16 units away.</returns>
    public static byte Next(byte exposure, Fs50ContactRegion contact, bool enabled, bool locked)
    {
        if (!enabled || locked || !contact.Present || contact.Signal is >= 175 and <= 205)
            return exposure;
        double target = Math.Round(
            exposure * Math.Pow(190 / Math.Max(contact.Signal, 1), 0.25),
            MidpointRounding.ToEven
        );
        return (byte)Math.Clamp(target, Math.Max(1, exposure - 16), Math.Min(255, exposure + 16));
    }
}
