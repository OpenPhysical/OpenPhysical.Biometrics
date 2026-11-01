// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Tool;

using System.Diagnostics.CodeAnalysis;
using CSharpFunctionalExtensions;

/// <summary>The single edge where IO, JSON, and parsing exceptions become command failures.</summary>
[SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Justification = "File system and serializer calls throw; this edge converts every failure to a result."
)]
internal static class ToolEdge
{
    internal static Result<T, ToolError> Try<T>(Func<T> call, ToolError failure)
    {
        try
        {
            return Result.Success<T, ToolError>(call());
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Result.Failure<T, ToolError>(failure);
        }
    }

    internal static Result<T, ToolError> TryBind<T>(
        Func<Result<T, ToolError>> call,
        ToolError failure
    ) => Try(call, failure).Bind(inner => inner);
}
