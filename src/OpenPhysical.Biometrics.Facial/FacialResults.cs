// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Facial;

internal static class FacialResults
{
    internal static Result<T, FacialError> Fail<T>(this FacialError error) =>
        Result.Failure<T, FacialError>(error);

    internal static Result<T, FacialError> Succeed<T>(this T value) =>
        Result.Success<T, FacialError>(value);
}
