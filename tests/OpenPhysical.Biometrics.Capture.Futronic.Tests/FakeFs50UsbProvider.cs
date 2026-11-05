// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Usb;

internal sealed class FakeFs50UsbProvider : IUsbDeviceProvider, IUsbDeviceTransport
{
    internal static UsbDeviceInfo Device { get; } = new(0x1491, 0x0050, 1, 2);
    private byte[] response = [];
    private int offset;
    internal byte[] Descriptor { get; set; }
    internal byte[] Controls { get; set; }
    internal byte[] Calibration { get; set; }
    internal int Closed { get; private set; }
    internal byte Exposure { get; private set; }
    internal bool FailCapture { get; set; }
    internal bool CancelCapture { get; set; }
    internal bool HoldCapture { get; set; }
    internal TaskCompletionSource Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Disposed { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal CancellationTokenSource Cancellation { get; } = new();

    internal FakeFs50UsbProvider()
    {
        using var fixture = Fs50GeometryFixture.Load();
        Descriptor = Fs50GeometryFixture.Descriptor(fixture.RootElement);
        Controls = new byte[64];
        Fs50GeometryFixture.Controls(fixture.RootElement).CopyTo(Controls, 0);
        Calibration = Fs50GeometryFixture.CalibrationBytes(fixture.RootElement);
    }

    public ValueTask<Result<ImmutableArray<UsbDeviceInfo>, CaptureError>> DiscoverAsync(
        ushort vendorId,
        ushort productId,
        CancellationToken cancellationToken
    ) =>
        ValueTask.FromResult(
            Result.Success<ImmutableArray<UsbDeviceInfo>, CaptureError>(
                ImmutableArray.Create(Device)
            )
        );

    public ValueTask<Result<IUsbDeviceTransport, CaptureError>> OpenAsync(
        UsbDeviceInfo device,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(Result.Success<IUsbDeviceTransport, CaptureError>(this));

    public async ValueTask<Result<int, CaptureError>> WriteAsync(
        byte endpoint,
        ImmutableArray<byte> data,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        offset = 0;
        response = data[0] switch
        {
            0xE0 => Descriptor,
            0x50 => Controls,
            0x9D => Calibration.AsSpan((data[2] | (data[3] << 8)) - 0x40, 64).ToArray(),
            _ => Enumerable.Repeat((byte)168, 1280 * 832).ToArray(),
        };
        if (data[0] == 0x6A)
        {
            Exposure = data[1];
            Started.TrySetResult();
            if (HoldCapture)
                await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (CancelCapture)
                Cancellation.Cancel();
            if (FailCapture)
                response = [];
        }
        return data.Length;
    }

    public ValueTask<Result<ImmutableArray<byte>, CaptureError>> ReadAsync(
        byte endpoint,
        int maximumBytes,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        int length = Math.Min(maximumBytes, response.Length - offset);
        var bytes = ImmutableArray.CreateRange(response.AsSpan(offset, length).ToArray());
        offset += length;
        return ValueTask.FromResult(Result.Success<ImmutableArray<byte>, CaptureError>(bytes));
    }

    public ValueTask DisposeAsync()
    {
        Closed++;
        Disposed.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
