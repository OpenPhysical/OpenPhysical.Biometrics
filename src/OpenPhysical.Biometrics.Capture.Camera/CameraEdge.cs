// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using CSharpFunctionalExtensions;

internal static class CameraEdge
{
    internal static async Task<Result<T, CameraError>> RunAsync<T>(
        Func<Task<Result<T, CameraError>>> operation
    )
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (DllNotFoundException)
        {
            return NativeDependency();
        }
        catch (EntryPointNotFoundException)
        {
            return NativeDependency();
        }
        catch (OperationCanceledException)
        {
            return new CameraError(CameraErrorCodes.Cancelled, "Camera acquisition was cancelled.");
        }
        catch (UnauthorizedAccessException)
        {
            return new CameraError(
                CameraErrorCodes.PermissionDenied,
                "Allow camera access in system privacy settings."
            );
        }
        catch (Exception)
        {
            return new CameraError(
                CameraErrorCodes.Unavailable,
                "The camera is unavailable. Check its connection and camera permissions."
            );
        }
    }

    internal static async Task<UnitResult<CameraError>> ApplyAsync(Func<Task> operation)
    {
        var result = await RunAsync(async () =>
            {
                await operation().ConfigureAwait(false);
                return Result.Success<bool, CameraError>(true);
            })
            .ConfigureAwait(false);
        return result.IsSuccess
            ? UnitResult.Success<CameraError>()
            : UnitResult.Failure(result.Error);
    }

    private static CameraError NativeDependency() =>
        new(
            CameraErrorCodes.NativeDependency,
            "The native camera runtime is unavailable. Reinstall the complete Studio application."
        );

    internal static Result<T, CameraError> Run<T>(Func<Result<T, CameraError>> operation)
    {
        try
        {
            return operation();
        }
        catch (Exception)
        {
            return new CameraError(
                CameraErrorCodes.InvalidFrame,
                "The camera returned an invalid image frame."
            );
        }
    }
}
