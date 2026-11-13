// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal sealed class FaceAutoCapture
{
    private Maybe<long> started;
    private Maybe<long> observed;
    private bool captured;
    internal bool Enabled { get; private set; }
    private TimeSpan delay = TimeSpan.FromSeconds(1);

    internal UnitResult<DesktopError> Configure(bool enabled, TimeSpan settlingDelay)
    {
        var valid = DesktopPreferences.CheckDelay(settlingDelay.TotalSeconds);
        if (valid.IsFailure)
            return valid.Error;
        if (Enabled != enabled || delay != settlingDelay)
        {
            Enabled = enabled;
            delay = settlingDelay;
            Reset();
        }
        return UnitResult.Success<DesktopError>();
    }

    internal bool Observe(FaceCaptureReadiness readiness)
    {
        var now = TimeProvider.System.GetTimestamp();
        if (!Enabled || !readiness.Ready)
        {
            Reset();
            return false;
        }
        if (captured)
            return false;
        if (
            observed.Match(
                value => TimeProvider.System.GetElapsedTime(value, now).TotalSeconds > 1,
                () => false
            )
        )
            started = default;
        started = started.HasValue ? started : now;
        observed = now;
        captured = TimeProvider.System.GetElapsedTime(started.Value, now) >= delay;
        return captured;
    }

    internal UnitResult<DesktopError> Reset()
    {
        started = observed = default;
        captured = false;
        return UnitResult.Success<DesktopError>();
    }
}
