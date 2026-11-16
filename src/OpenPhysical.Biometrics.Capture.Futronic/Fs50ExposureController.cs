// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>Explicit exposure recommendations; never mutates an acquired image or session.</summary>
public static class Fs50ExposureController
{
    private const int DefaultTargetSignal = 190;
    private const int SignalTolerance = 15;

    /// <summary>Proposes a bounded change only for substantial contact, unless locked.</summary>
    /// <param name="exposure">Current exposure, including manual zero and 255.</param>
    /// <param name="contact">Validated contact statistics from the current corrected frame.</param>
    /// <param name="enabled">Whether automatic exposure was explicitly enabled.</param>
    /// <param name="locked">Whether exposure is locked, including during a roll.</param>
    /// <returns>The next exposure, at most 16 units away.</returns>
    public static byte Next(byte exposure, Fs50ContactRegion contact, bool enabled, bool locked) =>
        Recommend(exposure, contact, enabled, locked, DefaultTargetSignal);

    /// <summary>Proposes a bounded acquisition change for an explicitly selected darkness target.</summary>
    /// <param name="exposure">Current exposure, including manual zero and 255.</param>
    /// <param name="contact">Validated statistics from the unchanged current corrected frame.</param>
    /// <param name="enabled">Whether automatic exposure is enabled.</param>
    /// <param name="locked">Whether exposure is locked.</param>
    /// <param name="targetSignal">Desired inverted-intensity percentile from one through 255, with a 15-unit deadband.</param>
    /// <returns>The bounded exposure recommendation, or an invalid-argument failure.</returns>
    public static Result<byte, CaptureError> Next(
        byte exposure,
        Fs50ContactRegion contact,
        bool enabled,
        bool locked,
        int targetSignal
    ) =>
        targetSignal is >= 1 and <= 255 && contact is not null
            ? Recommend(exposure, contact, enabled, locked, targetSignal)
            : new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Exposure control requires contact statistics and a darkness target from one through 255."
            );

    /// <summary>Recommends acquisition exposure from unchanged observed contact statistics.</summary>
    /// <param name="exposure">Current applied exposure.</param>
    /// <param name="statistics">Original selected coherent component statistics.</param>
    /// <param name="enabled">Whether automatic acquisition control is enabled.</param>
    /// <param name="locked">Whether exposure is locked.</param>
    /// <param name="targetSignal">Validated desired inverted intensity.</param>
    /// <returns>A bounded recommendation; callers separately require exact-frame spatial evidence.</returns>
    public static Result<byte, CaptureError> NextObserved(
        byte exposure,
        Fs50RegionStatistics statistics,
        bool enabled,
        bool locked,
        int targetSignal
    ) =>
        statistics is not null && statistics.Area > 0 && targetSignal is >= 1 and <= 255
            ? RecommendSignal(exposure, statistics.Signal, enabled, locked, targetSignal)
            : new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Original contact statistics and a valid darkness target are required."
            );

    private static byte Recommend(
        byte exposure,
        Fs50ContactRegion contact,
        bool enabled,
        bool locked,
        int targetSignal
    )
    {
        return !contact.Present
            ? exposure
            : RecommendSignal(exposure, contact.Signal, enabled, locked, targetSignal);
    }

    private static byte RecommendSignal(
        byte exposure,
        double signal,
        bool enabled,
        bool locked,
        int targetSignal
    )
    {
        if (!enabled || locked || Math.Abs(signal - targetSignal) <= SignalTolerance)
            return exposure;
        double target = Math.Round(
            exposure * Math.Pow(targetSignal / Math.Max(signal, 1), 0.25),
            MidpointRounding.ToEven
        );
        return (byte)Math.Clamp(target, Math.Max(1, exposure - 16), Math.Min(255, exposure + 16));
    }
}
