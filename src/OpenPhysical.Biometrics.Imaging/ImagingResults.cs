// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Imaging;

internal static class ImagingResults
{
    internal static Result<T, ImagingError> Fail<T>(this ImagingError error) =>
        Result.Failure<T, ImagingError>(error);

    internal static UnitResult<ImagingError> AsUnit<T>(this Result<T, ImagingError> result) =>
        result.IsSuccess ? UnitResult.Success<ImagingError>() : UnitResult.Failure(result.Error);
}
