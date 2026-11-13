// SPDX-License-Identifier: GPL-2.0-or-later
using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class StudioVerificationSession(IStudioVerificationBackendFactory factory)
    : ObservableObject
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Maybe<IStudioVerificationBackend> backend;
    private bool enabled;
    private bool busy;
    internal bool Enabled => enabled;
    internal bool Busy
    {
        get => busy;
        private set => SetProperty(ref busy, value);
    }

    internal UnitResult<DesktopError> Enable()
    {
        enabled = true;
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Disable()
    {
        enabled = false;
        return UnitResult.Success<DesktopError>();
    }

    internal async Task<Result<T, DesktopError>> RunAsync<T>(
        Func<IStudioVerificationBackend, CancellationToken, Task<Result<T, DesktopError>>> action,
        CancellationToken token
    )
    {
        if (!enabled || !await gate.WaitAsync(0))
            return Unavailable<T>();
        Busy = true;
        try
        {
            var guarded = await DesktopEdge.TryAsync(
                () => ExecuteAsync(action, token),
                "Verification.Unavailable",
                "This step could not finish."
            );
            return !enabled ? Unavailable<T>()
                : guarded.IsSuccess ? guarded.Value
                : new DesktopError(
                    guarded.Error.Code,
                    guarded.Error.Code == "Cancelled"
                        ? "Card check cancelled."
                        : "This step could not finish."
                );
        }
        finally
        {
            Busy = false;
            gate.Release();
        }
    }

    internal Task<Result<StudioVerificationState, DesktopError>> ApplyAndReadAsync(
        Func<IStudioVerificationBackend, CancellationToken, Task<UnitResult<DesktopError>>> action,
        CancellationToken token
    ) =>
        RunAsync(
            async (backend, ct) =>
            {
                var applied = await action(backend, ct);
                return applied.IsFailure
                    ? Result.Failure<StudioVerificationState, DesktopError>(applied.Error)
                    : await backend.ReadAsync(ct);
            },
            token
        );

    internal async Task<UnitResult<DesktopError>> CloseAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (backend.HasValue)
                await backend.Value.DisposeAsync();
            backend = Maybe<IStudioVerificationBackend>.None;
            return UnitResult.Success<DesktopError>();
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<Result<T, DesktopError>> ExecuteAsync<T>(
        Func<IStudioVerificationBackend, CancellationToken, Task<Result<T, DesktopError>>> action,
        CancellationToken token
    )
    {
        if (backend.HasNoValue)
        {
            var opened = await factory.OpenAsync(token);
            if (opened.IsFailure)
                return opened.Error;
            backend = Maybe.From(opened.Value);
        }
        if (!enabled || token.IsCancellationRequested)
            return Unavailable<T>();
        return await action(backend.Value, token);
    }

    private static Result<T, DesktopError> Unavailable<T>() =>
        new DesktopError("Verification.Inactive", "Open card checking before continuing.");
}
