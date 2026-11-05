// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Usb;

/// <summary>Read-only calibration and volatile FS50 acquisition commands, serialized per open device.</summary>
public sealed class Fs50Protocol
{
    private readonly Fs50ProtocolExchange exchange;

    /// <summary>Creates a protocol on an exclusively owned transport.</summary>
    /// <param name="transport">Device transport owned and disposed by the capture session.</param>
    public Fs50Protocol(IUsbDeviceTransport transport) =>
        exchange = new Fs50ProtocolExchange(transport);

    /// <summary>Reads the 64-byte sensor descriptor.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Descriptor bytes or a transport failure.</returns>
    public ValueTask<Result<ImmutableArray<byte>, CaptureError>> ReadDescriptorAsync(
        CancellationToken cancellationToken
    ) => exchange.ExecuteAsync(ImmutableArray.Create((byte)0xE0), 64, cancellationToken);

    /// <summary>Reads controls without writing device configuration.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Controls or a transport failure.</returns>
    public ValueTask<Result<ImmutableArray<byte>, CaptureError>> ReadControlsAsync(
        CancellationToken cancellationToken
    ) => exchange.ExecuteAsync(ImmutableArray.Create((byte)0x50), 64, cancellationToken);

    /// <summary>Captures the complete raw sensor frame, with no correction claim.</summary>
    /// <param name="exposure">The full supported range, including 255.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Raw 1280 by 832 grayscale bytes.</returns>
    public ValueTask<Result<ImmutableArray<byte>, CaptureError>> CaptureRawAsync(
        byte exposure,
        CancellationToken cancellationToken
    ) =>
        exchange.ExecuteAsync(
            ImmutableArray.Create((byte)0x6A, exposure),
            Fs50Descriptor.RawWidth * Fs50Descriptor.RawHeight,
            cancellationToken
        );

    /// <summary>Reads calibration twice and requires stable bytes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The stable calibration or a mismatch/transport failure.</returns>
    public async ValueTask<Result<ImmutableArray<byte>, CaptureError>> ReadCalibrationAsync(
        CancellationToken cancellationToken
    )
    {
        var first = await ReadCalibrationPassAsync(cancellationToken).ConfigureAwait(false);
        if (first.IsFailure)
        {
            return first;
        }

        var second = await ReadCalibrationPassAsync(cancellationToken).ConfigureAwait(false);
        if (second.IsFailure)
        {
            return second;
        }

        return first.Value.AsSpan().SequenceEqual(second.Value.AsSpan())
            ? first
            : Result.Failure<ImmutableArray<byte>, CaptureError>(
                new CaptureError(
                    CaptureErrorCodes.CalibrationInvalid,
                    "FS50 calibration changed between reads."
                )
            );
    }

    private async ValueTask<Result<ImmutableArray<byte>, CaptureError>> ReadCalibrationPassAsync(
        CancellationToken cancellationToken
    )
    {
        var bytes = new byte[4352];
        for (var address = 0x40; address < 0x1140; address += 64)
        {
            var command = ImmutableArray.Create(
                (byte)0x9D,
                (byte)0x40,
                (byte)address,
                (byte)(address >> 8)
            );
            var block = await exchange
                .ExecuteAsync(command, 64, cancellationToken)
                .ConfigureAwait(false);
            if (block.IsFailure)
            {
                return block;
            }

            block.Value.AsSpan().CopyTo(bytes.AsSpan(address - 0x40));
        }

        return ImmutableArray.CreateRange(bytes);
    }
}
