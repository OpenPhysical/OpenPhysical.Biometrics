// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Tool;

using CSharpFunctionalExtensions;

internal static class ToolResults
{
    internal static Result<T, ToolError> Fail<T>(this ToolError error) =>
        Result.Failure<T, ToolError>(error);

    internal static Result<T, ToolError> Succeed<T>(this T value) =>
        Result.Success<T, ToolError>(value);
}
