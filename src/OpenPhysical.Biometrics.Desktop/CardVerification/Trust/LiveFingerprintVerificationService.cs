// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.NBIS;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Inspection;
using Org.BouncyCastle.X509;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

/// <summary>Guides qualified acquisition against an authenticated reference on the original card lease.</summary>
/// <param name="live">Live.</param>
/// <param name="pinCeremony">Pin ceremony.</param>
/// <param name="fingerprints">Fingerprints.</param>
/// <param name="devices">Configured capture drivers.</param>
/// <param name="adapter">Adapter.</param>
/// <param name="clock">Trusted host clock.</param>
public sealed class LiveFingerprintVerificationService(
    LiveCredentialVerificationService live,
    PinCeremonyService pinCeremony,
    FingerprintCeremonyService fingerprints,
    CaptureDriverRegistry devices,
    AcquiredFingerprintAdapter adapter,
    TimeProvider clock
)
{
    /// <summary>Authenticates reference choices, acquires the selected finger, and finishes with fresh card proof.</summary>
    /// <remarks>The host owns the card lease and PIN buffer; guidance receives only signed finger/view choices.</remarks>
    /// <param name="session">The original exclusive card session.</param>
    /// <param name="request">Authenticated credential and workflow context.</param>
    /// <param name="contentSignerChain">Exact leaf-first content signer path.</param>
    /// <param name="deviceId">Approved capture device identity.</param>
    /// <param name="accessPin">Caller-owned PIN for protected reference access.</param>
    /// <param name="guide">Explicit selection among authenticated enrolled fingers.</param>
    /// <param name="cancellationToken">Cancellation for this operation.</param>
    /// <returns>The bounded operation result or a typed failure.</returns>
    public Task<Result<CredentialVerificationResult, VerificationError>> VerifyAsync(
        ILocalCardSession session,
        CredentialVerificationRequest request,
        ImmutableArray<X509Certificate> contentSignerChain,
        string deviceId,
        ReadOnlyMemory<byte> accessPin,
        Func<
            ImmutableArray<FingerprintReferenceSelection>,
            CancellationToken,
            Task<Result<FingerprintReferenceSelection, VerificationError>>
        > guide,
        CancellationToken cancellationToken
    )
    {
        if (guide is null)
            return Task.FromResult(
                Result.Failure<CredentialVerificationResult, VerificationError>(
                    new(VerificationErrorCodes.InvalidInput, "capture.guide")
                )
            );
        var policy = GuidedFingerprintCaptureService.ReadAcquisitionPolicy(request, deviceId);
        if (policy.IsFailure)
            return Task.FromResult(
                Result.Failure<CredentialVerificationResult, VerificationError>(policy.Error)
            );
        var operation = new FingerprintOperation(
            session,
            contentSignerChain,
            deviceId,
            accessPin,
            guide,
            policy.Value
        );
        return live.VerifyWithHolderAsync(
            session,
            request,
            (current, token) => CaptureAsync(operation, current, token),
            cancellationToken
        );
    }

    private async Task<Result<HolderVerificationEvidence, VerificationError>> CaptureAsync(
        FingerprintOperation operation,
        CredentialVerificationRequest request,
        CancellationToken token
    )
    {
        if (operation.Session.EnsureCurrent().IsFailure)
            return Failure();
        if (!operation.Session.Card.SecurityContext.IsAuthenticated)
        {
            var unlocked = pinCeremony.VerifyPinOnce(
                operation.Session.Card,
                operation.AccessPin,
                token
            );
            if (unlocked.IsFailure)
                return unlocked.Error;
        }
        if (operation.Session.EnsureCurrent().IsFailure)
            return Failure();
        var prepared = await fingerprints
            .PrepareAsync(request, operation.Session, operation.ContentSignerChain, token)
            .ConfigureAwait(false);
        if (prepared.IsFailure)
            return prepared.Error;
        return await new GuidedFingerprintCaptureService(fingerprints, devices, adapter, clock)
            .CaptureAsync(
                prepared.Value,
                request,
                operation.Session,
                operation.DeviceId,
                operation.Policy,
                operation.Guide,
                token
            )
            .ConfigureAwait(false);
    }

    private static Result<HolderVerificationEvidence, VerificationError> Failure() =>
        Result.Failure<HolderVerificationEvidence, VerificationError>(
            new(VerificationErrorCodes.EvidenceUnavailable, "capture.unavailable")
        );

    private sealed record FingerprintOperation(
        ILocalCardSession Session,
        ImmutableArray<X509Certificate> ContentSignerChain,
        string DeviceId,
        ReadOnlyMemory<byte> AccessPin,
        Func<
            ImmutableArray<FingerprintReferenceSelection>,
            CancellationToken,
            Task<Result<FingerprintReferenceSelection, VerificationError>>
        > Guide,
        GuidedFingerprintCaptureService.AcquisitionPolicy Policy
    );
}
