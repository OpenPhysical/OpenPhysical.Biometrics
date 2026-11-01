// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using CSharpFunctionalExtensions;

// The single boundary where caller-supplied services meet the Result-based workflow.
// Their exceptions and cancellation become failed results with stable codes.
internal static class ProviderEdge
{
    internal static async Task<Result<T, PivError>> CallAsync<T>(
        Func<Task<T>> call,
        CancellationToken cancellationToken,
        string failureCode
    )
    {
        if (cancellationToken.IsCancellationRequested)
            return Cancelled<T>();
        try
        {
            var value = await call().ConfigureAwait(false);
            return cancellationToken.IsCancellationRequested ? Cancelled<T>() : value;
        }
        catch (OperationCanceledException)
        {
            return Cancelled<T>();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Result.Failure<T, PivError>(
                new PivError(failureCode, "The external service failed.")
            );
        }
    }

    internal static Result<T, PivError> Cancelled<T>() =>
        Result.Failure<T, PivError>(
            new PivError(PivErrorCodes.Cancelled, "The operation was cancelled.")
        );
}
