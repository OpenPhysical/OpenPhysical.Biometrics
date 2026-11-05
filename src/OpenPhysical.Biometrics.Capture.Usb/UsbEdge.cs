// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Usb;

using CSharpFunctionalExtensions;

/// <summary>Converts native/driver failures once without copying their messages or payloads.</summary>
public static class UsbEdge
{
    /// <summary>Executes a USB operation at the exception boundary.</summary>
    /// <typeparam name="T">Successful operation value.</typeparam>
    /// <param name="operation">Bounded transport operation.</param>
    /// <param name="cancellationToken">Caller cancellation.</param>
    /// <returns>A typed safe result.</returns>
    public static async ValueTask<Result<T, CaptureError>> RunAsync<T>(
        Func<ValueTask<Result<T, CaptureError>>> operation,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Result.Failure<T, CaptureError>(
                    new CaptureError(CaptureErrorCodes.Cancelled, "USB operation cancelled.")
                );
            }

            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<T, CaptureError>(
                new CaptureError(CaptureErrorCodes.Cancelled, "USB operation cancelled.")
            );
        }
        catch (Exception)
        {
            return Result.Failure<T, CaptureError>(
                new CaptureError(CaptureErrorCodes.DeviceUnavailable, "USB operation failed.")
            );
        }
    }
}
