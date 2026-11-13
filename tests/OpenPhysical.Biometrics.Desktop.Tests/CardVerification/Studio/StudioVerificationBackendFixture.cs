// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Workstation.Application.Pairing;
using OpenPhysical.Workstation.Application.Verification;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal sealed class StudioVerificationBackendFixture : IStudioVerificationBackend
{
    internal StudioVerificationState State { get; set; } = StudioVerificationStateFixture.Ready;
    internal bool RejectRead { get; set; }
    internal bool HoldUntilReleased { get; set; }
    internal ImmutableArray<GuidedFingerChoice> FingerChoices { get; set; } = [];
    internal Maybe<StudioVerificationSelection> LastSelection { get; private set; }
    internal Maybe<GuidedFingerChoice> LastFinger { get; private set; }
    internal ReadOnlyMemory<byte> LastPin { get; private set; }
    internal int Verifications { get; private set; }
    internal int SignIns { get; private set; }
    internal int Synchronizations { get; private set; }
    internal int Disposals { get; private set; }
    internal bool DisposedBeforeDrain { get; private set; }
    internal TaskCompletionSource Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource GuidanceRequested { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Cancelled { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Result<CredentialVerificationResult, DesktopError> Outcome { get; set; } =
        new DesktopError("Verification.Denied", "The card check did not complete.");

    public Task<Result<StudioVerificationState, DesktopError>> ReadAsync(CancellationToken token) =>
        Task.FromResult(
            RejectRead
                ? Result.Failure<StudioVerificationState, DesktopError>(
                    new("Verification.Authorization", "Sign in again.")
                )
                : State
        );

    public Task<UnitResult<DesktopError>> PairAsync(PairingRequest request, CancellationToken token)
    {
        State = State with { Paired = true };
        return Task.FromResult(UnitResult.Success<DesktopError>());
    }

    public Task<UnitResult<DesktopError>> SignInAsync(string identity, CancellationToken token)
    {
        SignIns++;
        State = State with { SignedIn = true };
        return Task.FromResult(UnitResult.Success<DesktopError>());
    }

    public Task<UnitResult<DesktopError>> SynchronizeAsync(CancellationToken token)
    {
        Synchronizations++;
        State = State with { ConfigurationReady = true };
        return Task.FromResult(UnitResult.Success<DesktopError>());
    }

    public Task<UnitResult<DesktopError>> SignOutAsync(CancellationToken token)
    {
        State = State with { SignedIn = false };
        return Task.FromResult(UnitResult.Success<DesktopError>());
    }

    public Task<UnitResult<DesktopError>> UnpairAsync(CancellationToken token)
    {
        State = StudioVerificationState.Empty;
        return Task.FromResult(UnitResult.Success<DesktopError>());
    }

    public async Task<Result<CredentialVerificationResult, DesktopError>> VerifyAsync(
        StudioVerificationSelection selection,
        ReadOnlyMemory<byte> pin,
        ReadOnlyMemory<byte> privacyKey,
        Func<
            ImmutableArray<GuidedFingerChoice>,
            CancellationToken,
            Task<Result<GuidedFingerChoice, DesktopError>>
        > choose,
        CancellationToken token
    )
    {
        Verifications++;
        LastSelection = selection;
        LastPin = pin;
        using var cancellation = token.Register(() => Cancelled.TrySetResult());
        Started.TrySetResult();
        if (HoldUntilReleased)
            await Release.Task;
        if (token.IsCancellationRequested)
            return new DesktopError("Cancelled", "Card check cancelled.");
        if (!FingerChoices.IsEmpty)
        {
            var pending = choose(FingerChoices, token);
            GuidanceRequested.TrySetResult();
            var chosen = await pending;
            if (chosen.IsFailure)
                return chosen.Error;
            LastFinger = chosen.Value;
        }
        return Outcome;
    }

    public ValueTask DisposeAsync()
    {
        Disposals++;
        DisposedBeforeDrain = HoldUntilReleased && !Release.Task.IsCompleted;
        return ValueTask.CompletedTask;
    }
}
