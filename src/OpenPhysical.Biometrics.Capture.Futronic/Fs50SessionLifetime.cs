// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Usb;

internal sealed class Fs50SessionLifetime : IAsyncDisposable
{
    private readonly CancellationTokenSource timeout;
    private readonly CancellationTokenSource cancellation;
    private readonly object sync = new();
    private Maybe<Task> closing;
    private CancellationTokenRegistration registration;
    private int closed;

    internal Fs50SessionLifetime(TimeSpan duration, TimeProvider clock, CancellationToken token)
    {
        timeout = new CancellationTokenSource(duration, clock);
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, token);
    }

    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal CancellationToken Token => cancellation.Token;
    internal bool IsClosed =>
        Volatile.Read(ref closed) != 0 || cancellation.IsCancellationRequested;

    internal bool Observe(IUsbDeviceTransport transport)
    {
        registration = Token.Register(() =>
        {
            _ = CloseAsync(transport);
        });
        return true;
    }

    internal Task CloseAsync(IUsbDeviceTransport transport)
    {
        lock (sync)
        {
            if (!closing.HasValue)
            {
                Interlocked.Exchange(ref closed, 1);
                closing = CloseCoreAsync(transport);
            }
            return closing.Value;
        }
    }

    public ValueTask DisposeAsync()
    {
        registration.Unregister();
        cancellation.Dispose();
        timeout.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task CloseCoreAsync(IUsbDeviceTransport transport)
    {
        await Task.Yield();
        await cancellation.CancelAsync().ConfigureAwait(false);
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await transport.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
            await DisposeAsync().ConfigureAwait(false);
        }
    }
}
