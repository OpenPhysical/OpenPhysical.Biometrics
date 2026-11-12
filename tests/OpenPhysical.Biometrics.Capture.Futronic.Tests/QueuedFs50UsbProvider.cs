// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Usb;

internal sealed class QueuedFs50UsbProvider : IUsbDeviceProvider
{
    private readonly Queue<byte> replies = new();
    private readonly byte[] descriptor;
    private readonly byte[] controls;
    private readonly byte[] calibration;
    internal Maybe<CancellationTokenSource> CancelOnImageRead { get; set; }
    internal Maybe<CancellationTokenSource> CancelOnImageCommand { get; set; }
    internal int ImageReadTimeouts { get; set; }
    internal int ImageChunkLimit { get; init; } = 65536;
    internal bool NeverQuiet { get; init; }
    internal int ImageReads { get; private set; }
    internal int Reads { get; private set; }
    internal int Writes { get; private set; }
    internal int QueuedBytes => replies.Count;

    internal QueuedFs50UsbProvider()
    {
        using var fixture = Fs50GeometryFixture.Load();
        descriptor = Fs50GeometryFixture.Descriptor(fixture.RootElement);
        controls = new byte[64];
        Fs50GeometryFixture.Controls(fixture.RootElement).CopyTo(controls, 0);
        calibration = Fs50GeometryFixture.CalibrationBytes(fixture.RootElement);
    }

    public ValueTask<Result<ImmutableArray<UsbDeviceInfo>, CaptureError>> DiscoverAsync(
        ushort vendorId,
        ushort productId,
        CancellationToken cancellationToken
    ) =>
        ValueTask.FromResult(
            Result.Success<ImmutableArray<UsbDeviceInfo>, CaptureError>([
                FakeFs50UsbProvider.Device,
            ])
        );

    public ValueTask<Result<IUsbDeviceTransport, CaptureError>> OpenAsync(
        UsbDeviceInfo device,
        CancellationToken cancellationToken
    ) =>
        ValueTask.FromResult(
            Result.Success<IUsbDeviceTransport, CaptureError>(new Transport(this))
        );

    internal int InjectStale(int count)
    {
        foreach (var value in Enumerable.Repeat((byte)93, count))
            replies.Enqueue(value);
        return replies.Count;
    }

    private sealed class Transport(QueuedFs50UsbProvider owner) : IUsbDeviceTransport
    {
        private bool image;
        private bool cancelled;

        public ValueTask<Result<int, CaptureError>> WriteAsync(
            byte endpoint,
            ImmutableArray<byte> data,
            TimeSpan timeout,
            CancellationToken cancellationToken
        )
        {
            owner.Writes++;
            image = data[0] == 0x6A;
            var response = data[0] switch
            {
                0xE0 => owner.descriptor,
                0x50 => owner.controls,
                0x9D => owner.calibration.AsSpan((data[2] | data[3] << 8) - 0x40, 64).ToArray(),
                _ => Enumerable.Repeat((byte)168, 1280 * 832).ToArray(),
            };
            foreach (var value in response)
                owner.replies.Enqueue(value);
            if (image)
                owner.CancelOnImageCommand.Tap(value => value.Cancel());
            return ValueTask.FromResult(Result.Success<int, CaptureError>(data.Length));
        }

        public ValueTask<Result<ImmutableArray<byte>, CaptureError>> ReadAsync(
            byte endpoint,
            int maximumBytes,
            TimeSpan timeout,
            CancellationToken cancellationToken
        )
        {
            owner.Reads++;
            if (cancellationToken.IsCancellationRequested)
                return ValueTask.FromResult(
                    Result.Failure<ImmutableArray<byte>, CaptureError>(
                        new(CaptureErrorCodes.Cancelled, "Cancelled fake read.")
                    )
                );
            if (image && owner.ImageReadTimeouts > 0)
            {
                owner.ImageReadTimeouts--;
                return ValueTask.FromResult(
                    Result.Failure<ImmutableArray<byte>, CaptureError>(
                        new(CaptureErrorCodes.TransferTimedOut, "Image is still being produced.")
                    )
                );
            }
            if (owner.replies.Count == 0)
                return ValueTask.FromResult(
                    owner.NeverQuiet
                        ? Result.Success<ImmutableArray<byte>, CaptureError>([93])
                        : Result.Failure<ImmutableArray<byte>, CaptureError>(
                            new(CaptureErrorCodes.TransferTimedOut, "Quiet fake endpoint.")
                        )
                );
            return ValueTask.FromResult(
                Result.Success<ImmutableArray<byte>, CaptureError>(Take(maximumBytes))
            );
        }

        private ImmutableArray<byte> Take(int maximumBytes)
        {
            var count = Math.Min(
                Math.Min(maximumBytes, image ? owner.ImageChunkLimit : maximumBytes),
                owner.replies.Count
            );
            var bytes = ImmutableArray.CreateBuilder<byte>(count);
            for (var index = 0; index < count; index++)
                bytes.Add(owner.replies.Dequeue());
            if (image)
            {
                owner.ImageReads++;
                if (!cancelled && owner.CancelOnImageRead.HasValue)
                {
                    cancelled = true;
                    owner.CancelOnImageRead.Value.Cancel();
                }
            }
            return bytes.MoveToImmutable();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
