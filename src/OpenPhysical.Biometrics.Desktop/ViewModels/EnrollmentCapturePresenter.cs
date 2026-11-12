// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class EnrollmentCapturePresenter(Maybe<CaptureViewModel> live)
{
    internal bool HasActiveAttempt => live.Match(capture => capture.HasActiveAttempt, () => false);

    internal UnitResult<DesktopError> SelectFinger(FingerChoice finger)
    {
        live.Tap(capture => capture.SelectedFinger = finger);
        return UnitResult.Success<DesktopError>();
    }

    internal async Task<UnitResult<DesktopError>> ReviewOrResumeAsync(
        FingerChoice finger,
        Maybe<CapturedFingerprint> accepted
    )
    {
        SelectFinger(finger);
        if (live.HasNoValue)
            return UnitResult.Success<DesktopError>();
        if (accepted.HasValue)
            return await live.Value.ReviewAcceptedAsync(accepted.Value);
        if (live.Value.HasActiveAttempt)
            await live.Value.RetakeCommand.ExecuteAsync(default);
        return UnitResult.Success<DesktopError>();
    }

    internal async Task<UnitResult<DesktopError>> ResetAsync(FingerChoice finger)
    {
        var reset = live.HasValue
            ? await live.Value.ResetDefaultsAsync()
            : UnitResult.Success<DesktopError>();
        SelectFinger(finger);
        return reset;
    }

    internal async Task<UnitResult<DesktopError>> RetryAsync()
    {
        if (live.HasValue)
            await live.Value.ResetCaptureCommand.ExecuteAsync(default);
        return UnitResult.Success<DesktopError>();
    }

    internal async Task<UnitResult<DesktopError>> AdvanceAsync(bool requiresCapture)
    {
        if (live.HasValue && requiresCapture)
            await live.Value.RetakeCommand.ExecuteAsync(default);
        return UnitResult.Success<DesktopError>();
    }
}
