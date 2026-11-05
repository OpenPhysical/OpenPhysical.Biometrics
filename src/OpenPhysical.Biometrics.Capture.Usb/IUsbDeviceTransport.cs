// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Usb;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Exclusive opened USB device; implementations bound transfers and own native cleanup.</summary>
public interface IUsbDeviceTransport : IAsyncDisposable
{
    /// <summary>Writes one bulk transfer and reports the actual byte count.</summary>
    /// <param name="endpoint">Output endpoint address.</param>
    /// <param name="data">Immutable command bytes.</param>
    /// <param name="timeout">Finite per-transfer timeout.</param>
    /// <param name="cancellationToken">Cancellation; issued physical transfers may finish first.</param>
    /// <returns>Actual written length or a safe transport failure.</returns>
    ValueTask<Result<int, CaptureError>> WriteAsync(
        byte endpoint,
        ImmutableArray<byte> data,
        TimeSpan timeout,
        CancellationToken cancellationToken
    );

    /// <summary>Reads at most the requested bytes in one bounded bulk transfer.</summary>
    /// <param name="endpoint">Input endpoint address.</param>
    /// <param name="maximumBytes">Maximum returned bytes.</param>
    /// <param name="timeout">Finite per-transfer timeout.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Actual response bytes or a safe transport failure.</returns>
    ValueTask<Result<ImmutableArray<byte>, CaptureError>> ReadAsync(
        byte endpoint,
        int maximumBytes,
        TimeSpan timeout,
        CancellationToken cancellationToken
    );
}
