// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class WorkspaceSourceLifecycle(
    CaptureViewModel capture,
    EnrollmentViewModel enrollment
)
{
    private readonly SemaphoreSlim transition = new(1, 1);
    private long generation;
    private bool stopping;
    private bool active;
    private bool captureWorkspace;

    internal async Task<UnitResult<DesktopError>> ActivateAsync(bool enrollmentWorkspace)
    {
        var expected = Interlocked.Increment(ref generation);
        active = true;
        captureWorkspace = enrollmentWorkspace;
        // Interrupt an opening camera before waiting for the previous transition to drain.
        var paused = await PauseCoreAsync();
        await transition.WaitAsync();
        try
        {
            if (stopping)
                return new DesktopError("Capture.Closed", "The capture workspace has stopped.");
            if (expected != Volatile.Read(ref generation))
                return UnitResult.Success<DesktopError>();
            if (paused.IsFailure)
                return paused;
            var drained = await PauseCoreAsync();
            if (drained.IsFailure)
                return drained;
            if (stopping || !active || expected != Volatile.Read(ref generation))
                return UnitResult.Success<DesktopError>();
            return captureWorkspace ? await enrollment.ActivateAsync(true)
                : capture.Captured.HasValue ? UnitResult.Success<DesktopError>()
                : await capture.StartAsync(CancellationToken.None);
        }
        finally
        {
            transition.Release();
        }
    }

    internal Task<UnitResult<DesktopError>> PauseAsync()
    {
        Interlocked.Increment(ref generation);
        return PauseRequestedAsync();
    }

    private async Task<UnitResult<DesktopError>> PauseRequestedAsync()
    {
        active = false;
        var interrupted = await PauseCoreAsync();
        await transition.WaitAsync();
        try
        {
            var drained = await PauseCoreAsync();
            return interrupted.IsFailure ? interrupted : drained;
        }
        finally
        {
            transition.Release();
        }
    }

    internal async Task<UnitResult<DesktopError>> ClearAsync(Func<Task> clear)
    {
        var resume = active;
        var workspace = captureWorkspace;
        var expected = Interlocked.Increment(ref generation);
        var paused = await PauseRequestedAsync();
        await clear();
        if (paused.IsFailure)
            return paused;
        return resume && !stopping && expected == Volatile.Read(ref generation)
            ? await ActivateAsync(workspace)
            : UnitResult.Success<DesktopError>();
    }

    internal async Task<UnitResult<DesktopError>> StopAsync()
    {
        stopping = true;
        return await PauseAsync();
    }

    private async Task<UnitResult<DesktopError>> PauseCoreAsync()
    {
        var scanner = await capture.PauseSourceAsync();
        var section = await enrollment.ActivateAsync(false);
        return scanner.IsFailure ? scanner : section;
    }
}
