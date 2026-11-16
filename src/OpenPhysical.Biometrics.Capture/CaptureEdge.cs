// SPDX-License-Identifier: GPL-2.0-or-later

using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Capture;

// Driver exceptions cross into functional results only at this boundary.
internal static class CaptureEdge
{
    internal static async Task<Result<T, CaptureError>> RunAsync<T>(
        Func<Task<Result<T, CaptureError>>> operation
    )
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new CaptureError(CaptureErrorCodes.Cancelled, "Acquisition was cancelled.");
        }
        catch (Exception)
        {
            return new CaptureError(
                CaptureErrorCodes.DeviceUnavailable,
                "The acquisition driver is unavailable."
            );
        }
    }

    internal static async Task<UnitResult<CaptureError>> ApplyAsync(
        Func<Task<UnitResult<CaptureError>>> operation
    )
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return UnitResult.Failure(
                new CaptureError(CaptureErrorCodes.Cancelled, "Acquisition was cancelled.")
            );
        }
        catch (Exception)
        {
            return UnitResult.Failure(
                new CaptureError(
                    CaptureErrorCodes.DeviceUnavailable,
                    "The acquisition driver is unavailable."
                )
            );
        }
    }

    internal static async Task<UnitResult<CaptureError>> DisposeAsync(IAsyncDisposable session)
    {
        try
        {
            await session.DisposeAsync().ConfigureAwait(false);
            return UnitResult.Success<CaptureError>();
        }
        catch (Exception)
        {
            return UnitResult.Failure(
                new CaptureError(
                    CaptureErrorCodes.DeviceUnavailable,
                    "The device could not be closed safely."
                )
            );
        }
    }
}
