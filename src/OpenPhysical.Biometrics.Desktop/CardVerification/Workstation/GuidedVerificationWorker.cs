// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Infrastructure.Trust;

namespace OpenPhysical.Workstation.Application.Verification;

internal sealed class GuidedVerificationWorker(IGuidedVerificationRunner runner, object sync)
{
    internal async Task RunAsync(
        GuidedVerificationOperation operation,
        GuidedVerificationRequest request,
        VerifiedBundleSnapshot bundle,
        byte[] pin,
        byte[] key
    )
    {
        try
        {
            var result = await CaptureVerificationEdge
                .RunAsync(
                    () =>
                        runner.VerifyAsync(
                            request,
                            bundle,
                            pin,
                            key,
                            (choices, token) => GuideAsync(operation, choices, token),
                            operation.Cancellation.Token
                        ),
                    "guidance.unavailable"
                )
                .ConfigureAwait(false);
            Observe(operation, result);
        }
        finally
        {
            Array.Clear(pin);
            Array.Clear(key);
            lock (sync)
                operation.Choices = ImmutableArray<GuidedFingerChoice>.Empty;
        }
    }

    private void Observe(
        GuidedVerificationOperation operation,
        Result<CredentialVerificationResult, VerificationError> result
    )
    {
        lock (sync)
        {
            if (operation.Cancellation.IsCancellationRequested)
                FinishFailure(operation, VerificationErrorCodes.Cancelled);
            else if (result.IsFailure)
                FinishFailure(operation, result.Error.Code);
            else
            {
                operation.Phase = GuidedVerificationPhase.Completed;
                operation.Result = result.Value;
            }
        }
    }

    private async Task<Result<GuidedFingerChoice, VerificationError>> GuideAsync(
        GuidedVerificationOperation operation,
        ImmutableArray<GuidedFingerChoice> choices,
        CancellationToken token
    )
    {
        lock (sync)
        {
            if (
                choices.IsDefaultOrEmpty
                || choices.Length > 32
                || operation.Phase != GuidedVerificationPhase.Preparing
                || operation.Cancellation.IsCancellationRequested
            )
                return Failure<GuidedFingerChoice>(VerificationErrorCodes.EvidenceUnavailable);
            operation.Choices = choices;
            operation.Phase = GuidedVerificationPhase.AwaitingFinger;
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            token,
            operation.Cancellation.Token
        );
        return await CaptureVerificationEdge
            .RunAsync(
                async () =>
                    Result.Success<GuidedFingerChoice, VerificationError>(
                        await operation.Selection.Task.WaitAsync(linked.Token).ConfigureAwait(false)
                    ),
                "guidance.unavailable"
            )
            .ConfigureAwait(false);
    }

    private static void FinishFailure(
        GuidedVerificationOperation operation,
        VerificationErrorCodes code
    )
    {
        operation.Phase = GuidedVerificationPhase.Failed;
        operation.Error = code;
    }

    private static Result<T, VerificationError> Failure<T>(VerificationErrorCodes code) =>
        Result.Failure<T, VerificationError>(new(code, "guidance.unavailable"));
}
