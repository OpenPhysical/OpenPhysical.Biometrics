// SPDX-License-Identifier: GPL-2.0-or-later
using System.Diagnostics.CodeAnalysis;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Imaging;

/// <summary>The single edge where exceptions from SkiaSharp and the file system become results.</summary>
[SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Justification = "Third-party Skia and IO calls throw; this edge converts every failure to a result."
)]
internal static class ImagingGuard
{
    internal static Result<T, ImagingError> Try<T>(Func<T> operation, string code)
    {
        try
        {
            return Result.Success<T, ImagingError>(operation());
        }
        catch (Exception exception)
        {
            return Result.Failure<T, ImagingError>(Describe(exception, code));
        }
    }

    internal static Result<T, ImagingError> TryBind<T>(
        Func<Result<T, ImagingError>> operation,
        string code
    ) => Try(operation, code).Bind(inner => inner);

    internal static UnitResult<ImagingError> TryUnit(
        Func<UnitResult<ImagingError>> operation,
        string code
    )
    {
        var outer = Try(operation, code);
        return outer.IsSuccess ? outer.Value : UnitResult.Failure(outer.Error);
    }

    internal static async Task<Result<T, ImagingError>> TryAsync<T>(
        Func<Task<Result<T, ImagingError>>> operation,
        string code
    )
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return Result.Failure<T, ImagingError>(Describe(exception, code));
        }
    }

    private static ImagingError Describe(Exception exception, string code) =>
        exception is OperationCanceledException
            ? ImagingErrors.Cancelled()
            : new ImagingError(code, exception.Message);
}
