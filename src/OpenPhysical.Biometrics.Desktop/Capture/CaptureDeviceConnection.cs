// SPDX-License-Identifier: GPL-2.0-or-later

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class CaptureDeviceConnection(CaptureDriverRegistry registry, TimeProvider clock)
{
    private Maybe<IFingerprintCaptureSession> session;
    private long openedAt;

    internal Maybe<IFingerprintCaptureSession> Session => session;

    internal bool IsCurrent =>
        session.HasValue && clock.GetElapsedTime(openedAt) < TimeSpan.FromMinutes(5);

    internal async Task<UnitResult<DesktopError>> EnsureOpenAsync(
        CaptureSettings settings,
        CancellationToken token
    )
    {
        if (IsCurrent)
            return UnitResult.Success<DesktopError>();
        var closed = await CloseAsync().ConfigureAwait(false);
        if (closed.IsFailure)
            return closed;
        openedAt = clock.GetTimestamp();
        var devices = await registry.DiscoverAsync(token).ConfigureAwait(false);
        if (devices.IsFailure)
            return new DesktopError(devices.Error.Code, devices.Error.Message);
        if (devices.Value.Length != 1)
            return new DesktopError(
                "Capture.DeviceUnavailable",
                devices.Value.IsEmpty ? "Connect an FS50 scanner." : "Connect one FS50 scanner."
            );
        var opened = await registry
            .OpenAsync(devices.Value[0], settings, token)
            .ConfigureAwait(false);
        if (opened.IsFailure)
            return new DesktopError(opened.Error.Code, opened.Error.Message);
        session = Maybe.From(opened.Value);
        return UnitResult.Success<DesktopError>();
    }

    internal async Task<UnitResult<DesktopError>> CloseAsync()
    {
        if (!session.HasValue)
            return UnitResult.Success<DesktopError>();
        var current = session.Value;
        session = Maybe<IFingerprintCaptureSession>.None;
        var closed = await DesktopEdge
            .TryAsync(
                async () =>
                {
                    await current.DisposeAsync().ConfigureAwait(false);
                    return true;
                },
                "Capture.Close",
                "The scanner could not be released."
            )
            .ConfigureAwait(false);
        return closed.IsSuccess
            ? UnitResult.Success<DesktopError>()
            : UnitResult.Failure(closed.Error);
    }
}
