// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;

namespace OpenPhysical.Workstation.Application.Verification;

/// <summary>Retains one bounded guided operation until its worker actually releases the card and capture resources.</summary>
/// <param name="runner">Capture-side verification worker.</param>
/// <param name="clock">Trusted host clock.</param>
public sealed class GuidedVerificationCoordinator(
    IGuidedVerificationRunner runner,
    TimeProvider clock
) : IAsyncDisposable
{
    private readonly object sync = new();
    private GuidedVerificationOperation? current;
    private bool stopped;
    private Task? disposal;

    /// <summary>Starts one policy-bound capture operation with owned secret buffers.</summary>
    /// <param name="request">Authenticated credential and workflow context.</param>
    /// <param name="bundle">Authenticated signed configuration.</param>
    /// <param name="pin">Caller-owned PIN bytes.</param>
    /// <param name="privacyKey">Caller-owned TWIC privacy key.</param>
    /// <returns>The bounded operation result or a typed failure.</returns>
    public Result<Guid, VerificationError> Start(
        GuidedVerificationRequest request,
        VerifiedBundleSnapshot bundle,
        ReadOnlySpan<byte> pin,
        ReadOnlySpan<byte> privacyKey
    )
    {
        if (
            !GuidedVerificationInputs.IsValid(request)
            || pin.Length > 8
            || privacyKey.Length > 1024
            || (request.Kind == GuidedCredentialKind.Twic && !pin.IsEmpty)
            || (request.Kind == GuidedCredentialKind.Piv && !privacyKey.IsEmpty)
            || (
                request.Kind == GuidedCredentialKind.Twic
                && request.InterfaceType == PivInterfaceType.Contactless
                && privacyKey.IsEmpty
            )
            || !bundle.Policy.AllowedProfileIds.Contains(request.ProfileId)
        )
            return Failure<Guid>(VerificationErrorCodes.InvalidInput);
        lock (sync)
        {
            return StartLocked(request, bundle, pin, privacyKey);
        }
    }

    private Result<Guid, VerificationError> StartLocked(
        GuidedVerificationRequest request,
        VerifiedBundleSnapshot bundle,
        ReadOnlySpan<byte> pin,
        ReadOnlySpan<byte> privacyKey
    )
    {
        if (stopped || current is not null && !current.Work.IsCompleted)
            return Failure<Guid>(VerificationErrorCodes.EvidenceUnavailable);
        current?.Cancellation.Dispose();
        var operation = new GuidedVerificationOperation(
            Guid.NewGuid(),
            clock.GetUtcNow().AddMinutes(3),
            bundle.ManifestSha256,
            clock
        );
        var ownedPin = pin.ToArray();
        var ownedKey = privacyKey.ToArray();
        current = operation;
        operation.Work = Task.Run(() =>
            new GuidedVerificationWorker(runner, sync).RunAsync(
                operation,
                request,
                bundle,
                ownedPin,
                ownedKey
            )
        );
        return operation.Id;
    }

    /// <summary>Reads progress without advancing or restarting capture.</summary>
    /// <param name="id">Host-issued operation identity.</param>
    /// <returns>The bounded operation result or a typed failure.</returns>
    public Maybe<GuidedVerificationStatus> Read(Guid id)
    {
        lock (sync)
        {
            if (
                current is null
                || current.Id != id
                || clock.GetUtcNow() > current.ExpiresAt.AddMinutes(2)
            )
                return Maybe<GuidedVerificationStatus>.None;
            return new GuidedVerificationStatus(
                current.Id,
                current.Phase,
                current.ExpiresAt,
                current.BundleSha256,
                current.Choices,
                current.Result,
                current.Error
            );
        }
    }

    /// <summary>Accepts only a finger authenticated from the card reference.</summary>
    /// <param name="id">Host-issued operation identity.</param>
    /// <param name="choice">One authenticated enrolled finger and view.</param>
    /// <returns>The bounded operation result or a typed failure.</returns>
    public UnitResult<VerificationError> Select(Guid id, GuidedFingerChoice choice)
    {
        lock (sync)
        {
            if (
                current is null
                || current.Id != id
                || current.Phase != GuidedVerificationPhase.AwaitingFinger
                || current.Cancellation.IsCancellationRequested
                || clock.GetUtcNow() >= current.ExpiresAt
                || !current.Choices.Contains(choice)
            )
                return UnitResult.Failure(
                    new VerificationError(
                        VerificationErrorCodes.InvalidInput,
                        "guidance.selection-invalid"
                    )
                );
            current.Phase = GuidedVerificationPhase.Capturing;
            current.Choices = ImmutableArray<GuidedFingerChoice>.Empty;
            current.Selection.TrySetResult(choice);
            return UnitResult.Success<VerificationError>();
        }
    }

    /// <summary>Requests cancellation while retaining ownership until the worker drains.</summary>
    /// <param name="id">Host-issued operation identity.</param>
    /// <returns>The bounded operation result or a typed failure.</returns>
    public bool Cancel(Guid id)
    {
        lock (sync)
        {
            if (current is null || current.Id != id || current.Work.IsCompleted)
                return false;
            current.Phase = GuidedVerificationPhase.Cancelling;
            current.Choices = ImmutableArray<GuidedFingerChoice>.Empty;
            current.Cancellation.Cancel();
            return true;
        }
    }

    /// <summary>Waits until capture resources and owned secrets are released.</summary>
    /// <returns>The bounded operation result or a typed failure.</returns>
    public ValueTask DisposeAsync()
    {
        lock (sync)
        {
            if (disposal is not null)
                return new ValueTask(disposal);
            stopped = true;
            current?.Cancellation.Cancel();
            disposal = DrainAsync(current);
            return new ValueTask(disposal);
        }
    }

    private static async Task DrainAsync(GuidedVerificationOperation? operation)
    {
        if (operation is null)
            return;
        await operation.Work.ConfigureAwait(false);
        operation.Cancellation.Dispose();
    }

    private static Result<T, VerificationError> Failure<T>(VerificationErrorCodes code) =>
        Result.Failure<T, VerificationError>(new(code, "guidance.unavailable"));
}
