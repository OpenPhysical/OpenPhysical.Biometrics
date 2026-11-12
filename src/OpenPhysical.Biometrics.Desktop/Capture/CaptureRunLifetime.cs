// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal sealed class CaptureRunLifetime(CapturePreviewPump pump, CancellationToken shutdown)
    : IDisposable
{
    private Maybe<CancellationTokenSource> lifetime;
    private Maybe<Task> running;

    internal bool IsRunning => running.HasValue && !running.Value.IsCompleted;

    internal bool Start(CancellationToken appLifetime, Func<bool> begin)
    {
        if (!lifetime.HasValue)
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(appLifetime, shutdown);
        _ = begin();
        running = Task.Run(() => pump.RunAsync(lifetime.Value.Token));
        return true;
    }

    internal Task WaitAsync() => running.HasValue ? running.Value : Task.CompletedTask;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (lifetime.HasValue)
            lifetime.Value.Dispose();
    }
}
