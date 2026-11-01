// SPDX-License-Identifier: GPL-2.0-or-later
using System.Diagnostics.CodeAnalysis;
using CSharpFunctionalExtensions;
using Microsoft.ML.OnnxRuntime;

namespace OpenPhysical.Biometrics.Facial;

/// <summary>The single edge where exceptions from ONNX Runtime, CoreJ2K, SkiaSharp, and IO become results.</summary>
[SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Justification = "Third-party native and IO calls throw; this edge converts every failure to a result."
)]
internal static class FacialEdge
{
    internal static Result<T, FacialError> Try<T>(Func<T> call, string code, string message)
    {
        try
        {
            return Result.Success<T, FacialError>(call());
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Result.Failure<T, FacialError>(Describe(error, code, message));
        }
    }

    internal static Result<T, FacialError> TryBind<T>(
        Func<Result<T, FacialError>> call,
        string code,
        string message
    ) => Try(call, code, message).Bind(inner => inner);

    private static FacialError Describe(Exception error, string code, string message) =>
        error switch
        {
            DllNotFoundException
            or EntryPointNotFoundException
            or BadImageFormatException
            or TypeInitializationException => new FacialError(
                FacialErrorCodes.NativeRuntimeUnavailable,
                "The native model or image runtime is unavailable."
            ),
            OnnxRuntimeException => new FacialError(
                FacialErrorCodes.ModelExecutionFailed,
                "The model runtime failed."
            ),
            _ => new FacialError(code, message),
        };
}
