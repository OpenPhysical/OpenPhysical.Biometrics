// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using Avalonia.Threading;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

/// <summary>Forgets package protection and pauses acquisition when the local operator session becomes inactive.</summary>
public sealed class BiometricPrivacySession : IDisposable
{
    private readonly PackageProtectionViewModel protection;
    private readonly Func<Task<UnitResult<DesktopError>>> pause;
    private readonly ILogger<BiometricPrivacySession> logger;
    private readonly object pauseGate = new();
    private Maybe<MacPrivacyNotifications> notifications;
    private Task<UnitResult<DesktopError>> pending = Task.FromResult(
        UnitResult.Success<DesktopError>()
    );
    private int disposed;

    /// <summary>Creates an application-owned privacy session without starting native observation.</summary>
    /// <param name="protection">Transient package-passphrase owner.</param>
    /// <param name="pause">UI-thread callback that pauses biometric acquisition.</param>
    /// <param name="logger">Operational status logger without biometric contents.</param>
    public BiometricPrivacySession(
        PackageProtectionViewModel protection,
        Func<Task<UnitResult<DesktopError>>> pause,
        ILogger<BiometricPrivacySession> logger
    )
    {
        this.protection = protection;
        this.pause = pause;
        this.logger = logger;
    }

    /// <summary>Starts macOS screen-lock and workspace-session observation on the application UI thread.</summary>
    /// <returns>A subscription outcome, including an explicit unsupported-platform error elsewhere.</returns>
    public UnitResult<DesktopError> StartMonitoring()
    {
        if (Volatile.Read(ref disposed) != 0)
            return new DesktopError("Privacy.SessionDisposed", "The privacy session has ended.");
        if (notifications.HasValue)
            return UnitResult.Success<DesktopError>();
        var started = MacPrivacyNotifications.Create(NotifyNative);
        if (started.IsFailure)
            return started.Error;
        notifications = started.Value;
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Forgets the passphrase immediately, then drains the supplied acquisition pause.</summary>
    /// <returns>The pause outcome after protection has already been locked.</returns>
    public Task<UnitResult<DesktopError>> SuspendAsync()
    {
        protection.Lock();
        if (Volatile.Read(ref disposed) != 0)
            return Task.FromResult(UnitResult.Success<DesktopError>());
        return BeginPause();
    }

    /// <summary>Waits for the last dispatched acquisition pause during orderly shutdown.</summary>
    /// <returns>The dispatched pause outcome.</returns>
    public Task<UnitResult<DesktopError>> DrainAsync()
    {
        lock (pauseGate)
            return pending;
    }

    private UnitResult<DesktopError> NotifyNative()
    {
        if (Volatile.Read(ref disposed) != 0)
            return UnitResult.Success<DesktopError>();
        protection.Lock();
        Dispatcher.UIThread.Post(() =>
        {
            if (Volatile.Read(ref disposed) == 0)
                _ = BeginPause();
        });
        return UnitResult.Success<DesktopError>();
    }

    private Task<UnitResult<DesktopError>> BeginPause()
    {
        lock (pauseGate)
        {
            if (Volatile.Read(ref disposed) != 0 || !pending.IsCompleted)
                return pending;
            var completed = new TaskCompletionSource<UnitResult<DesktopError>>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            pending = completed.Task;
            _ = CompletePauseAsync(completed);
            return pending;
        }
    }

    private async Task<UnitResult<DesktopError>> CompletePauseAsync(
        TaskCompletionSource<UnitResult<DesktopError>> completed
    )
    {
        var result = await PauseAsync();
        completed.TrySetResult(result);
        return result;
    }

    private async Task<UnitResult<DesktopError>> PauseAsync()
    {
        var result = await DesktopEdge.TryAsync(
            pause,
            "Privacy.PauseFailed",
            "Biometric acquisition could not be paused."
        );
        var paused = result.IsFailure ? UnitResult.Failure(result.Error) : result.Value;
        if (paused.IsFailure)
            logger.LogWarning(
                "Biometric acquisition pause failed with code {Code}.",
                paused.Error.Code
            );
        return paused;
    }

    /// <summary>Unsubscribes native notifications and forgets the passphrase without resuming acquisition.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        notifications.Tap(value => value.Dispose());
        notifications = Maybe<MacPrivacyNotifications>.None;
        protection.Lock();
    }
}
