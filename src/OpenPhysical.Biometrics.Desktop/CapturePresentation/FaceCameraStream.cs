// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using Avalonia.Threading;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Camera;

internal sealed class FaceCameraStream(
    ICameraProvider provider,
    Func<CameraFrame, UnitResult<DesktopError>> present,
    Func<CameraFrame, Task<UnitResult<DesktopError>>> inspect,
    Func<CameraError, UnitResult<DesktopError>> error
)
{
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private readonly object ownership = new();
    private Maybe<CancellationTokenSource> cancellation;
    private Task running = Task.CompletedTask;
    private int generation;

    internal async Task<UnitResult<DesktopError>> StartAsync(CameraSource source, CameraMode mode)
    {
        await lifecycle.WaitAsync();
        try
        {
            await DrainAsync();
            var owned = new CancellationTokenSource();
            var token = owned.Token;
            lock (ownership)
                cancellation = owned;
            var expected = Interlocked.Increment(ref generation);
            var opened = await provider.OpenAsync(source.Id, mode, token).ConfigureAwait(false);
            if (opened.IsFailure)
                return new DesktopError(opened.Error.Code, opened.Error.Message);
            if (token.IsCancellationRequested)
            {
                await opened.Value.DisposeAsync().ConfigureAwait(false);
                return new DesktopError("Cancelled", "Camera capture was cancelled.");
            }
            running = RunGuardedAsync(opened.Value, expected, token);
            return UnitResult.Success<DesktopError>();
        }
        finally
        {
            lifecycle.Release();
        }
    }

    private async Task RunAsync(ICameraSession session, int expected, CancellationToken token)
    {
        var analyzed = TimeProvider.System.GetTimestamp();
        try
        {
            while (!token.IsCancellationRequested)
            {
                var read = await session.ReadAsync(token).ConfigureAwait(false);
                if (read.IsFailure)
                {
                    await ReportAsync(read.Error, expected, token);
                    return;
                }
                var shown = await PresentAsync(read.Value, expected, token);
                if (shown.IsFailure)
                {
                    await ReportAsync(new(shown.Error.Code, shown.Error.Message), expected, token);
                    return;
                }
                if (
                    expected == Volatile.Read(ref generation)
                    && !token.IsCancellationRequested
                    && TimeProvider.System.GetElapsedTime(analyzed)
                        >= TimeSpan.FromMilliseconds(333)
                )
                {
                    analyzed = TimeProvider.System.GetTimestamp();
                    await inspect(read.Value).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<UnitResult<DesktopError>> PresentAsync(
        CameraFrame frame,
        int expected,
        CancellationToken token
    ) =>
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (expected != Volatile.Read(ref generation) || token.IsCancellationRequested)
            {
                frame.Dispose();
                return UnitResult.Success<DesktopError>();
            }
            return present(frame);
        });

    private async Task RunGuardedAsync(
        ICameraSession session,
        int expected,
        CancellationToken token
    )
    {
        var result = await DesktopEdge.TryAsync(
            async () =>
            {
                await RunAsync(session, expected, token).ConfigureAwait(false);
                return true;
            },
            "Face.Source",
            "The camera stopped unexpectedly. Reconnect it and try again."
        );
        if (result.IsFailure)
            await ReportAsync(new(result.Error.Code, result.Error.Message), expected, token);
    }

    private async Task<UnitResult<DesktopError>> ReportAsync(
        CameraError problem,
        int expected,
        CancellationToken token
    ) =>
        await Dispatcher.UIThread.InvokeAsync(() =>
            expected == Volatile.Read(ref generation) && !token.IsCancellationRequested
                ? error(problem)
                : UnitResult.Success<DesktopError>()
        );

    internal async Task<UnitResult<DesktopError>> StopAsync()
    {
        Interlocked.Increment(ref generation);
        Task cancelled;
        lock (ownership)
            cancelled = cancellation.Match(value => value.CancelAsync(), () => Task.CompletedTask);
        await cancelled;
        await lifecycle.WaitAsync();
        try
        {
            await DrainAsync();
            return UnitResult.Success<DesktopError>();
        }
        finally
        {
            lifecycle.Release();
        }
    }

    private async Task DrainAsync()
    {
        Interlocked.Increment(ref generation);
        Maybe<CancellationTokenSource> previous;
        lock (ownership)
        {
            previous = cancellation;
            cancellation = default;
        }
        if (previous.HasValue)
            await previous.Value.CancelAsync();
        await running;
        previous.Tap(value => value.Dispose());
        running = Task.CompletedTask;
    }
}
