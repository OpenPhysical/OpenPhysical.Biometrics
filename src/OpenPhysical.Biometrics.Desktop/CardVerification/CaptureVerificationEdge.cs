// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Desktop;
using OpenPhysical.Middleware.Domain.Trust;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

// The shared desktop edge converts exceptions once; callers retain stable non-secret verification reasons.
internal static class CaptureVerificationEdge
{
    internal static async Task<Result<T, VerificationError>> RunAsync<T>(
        Func<Task<Result<T, VerificationError>>> operation,
        string reason
    ) => await RunAsync(operation, reason, reason).ConfigureAwait(false);

    internal static async Task<Result<T, VerificationError>> RunAsync<T>(
        Func<Task<Result<T, VerificationError>>> operation,
        string reason,
        string cancellationReason
    )
    {
        var attempted = await DesktopEdge
            .TryAsync(operation, "Verification.Unavailable", "Verification could not finish.")
            .ConfigureAwait(false);
        return attempted.IsSuccess
            ? attempted.Value
            : Result.Failure<T, VerificationError>(
                new(
                    attempted.Error.Code == "Cancelled"
                        ? VerificationErrorCodes.Cancelled
                        : VerificationErrorCodes.EvidenceUnavailable,
                    attempted.Error.Code == "Cancelled" ? cancellationReason : reason
                )
            );
    }
}
