// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.CardEdge.Credential;
using OpenPhysical.Middleware.Diagnostics;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure.CredentialSnapshots;
using OpenPhysical.Middleware.Infrastructure.Inspection;
using OpenPhysical.Middleware.Infrastructure.Twic;
using OpenPhysical.Middleware.Inspection;
using OpenPhysical.Middleware.Twic.Codec;
using ApplicationId = OpenPhysical.CardEdge.Application.ApplicationId;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

/// <summary>Runs guided TWIC biometric verification under the selected signed generation and MARSEC policy.</summary>
/// <param name="authority">Authority.</param>
/// <param name="verifier">Verifier.</param>
/// <param name="fingerprints">Fingerprints.</param>
/// <param name="devices">Configured capture drivers.</param>
/// <param name="adapter">Adapter.</param>
/// <param name="executor">Executor.</param>
/// <param name="clock">Trusted host clock.</param>
public sealed class LiveTwicFingerprintVerificationService(
    CredentialCeremonyService authority,
    TwicAcceptanceService verifier,
    FingerprintCeremonyService fingerprints,
    CaptureDriverRegistry devices,
    AcquiredFingerprintAdapter adapter,
    PivApduExecutor executor,
    TimeProvider clock
)
{
    /// <summary>Retains one exclusive lease through TWIC object authentication, capture and final cancellation evaluation.</summary>
    /// <remarks>The host clears a supplied TPK. When absent, only contact acquisition is permitted; owned key bytes are cleared. No subject PIN is sent.</remarks>
    /// <param name="session">The original exclusive card session.</param>
    /// <param name="request">Authenticated credential and workflow context.</param>
    /// <param name="profile">Exact authenticated TWIC workflow profile.</param>
    /// <param name="marsecLevel">Current MARSEC level for signed cancellation policy.</param>
    /// <param name="privacyKey">Caller-owned TWIC privacy key.</param>
    /// <param name="deviceId">Approved capture device identity.</param>
    /// <param name="guide">Explicit selection among authenticated enrolled fingers.</param>
    /// <param name="cancellationToken">Cancellation for this operation.</param>
    /// <returns>The bounded operation result or a typed failure.</returns>
    public Task<Result<CredentialVerificationResult, VerificationError>> VerifyAsync(
        ILocalCardSession session,
        CredentialVerificationRequest request,
        TwicAcceptanceProfile profile,
        int marsecLevel,
        Maybe<TwicPrivacyKeyContainer> privacyKey,
        string deviceId,
        Func<
            ImmutableArray<FingerprintReferenceSelection>,
            CancellationToken,
            Task<Result<FingerprintReferenceSelection, VerificationError>>
        > guide,
        CancellationToken cancellationToken
    )
    {
        if (session is null || request is null || profile is null || guide is null)
            return Task.FromResult(Failure("twic.live-context-unavailable"));
        var input = new TwicLiveVerificationInput(
            session,
            request,
            profile,
            marsecLevel,
            privacyKey,
            deviceId,
            guide,
            cancellationToken
        );
        return CaptureVerificationEdge.RunAsync(
            () => VerifyGuardedAsync(input),
            "twic.live-unavailable",
            "twic.live-cancelled"
        );
    }

    private async Task<Result<CredentialVerificationResult, VerificationError>> VerifyGuardedAsync(
        TwicLiveVerificationInput input
    )
    {
        input.Token.ThrowIfCancellationRequested();
        if (!input.ValidContext)
            return Failure("twic.live-context-unavailable");
        var policy = GuidedFingerprintCaptureService.ReadAcquisitionPolicy(
            input.Request,
            input.DeviceId
        );
        if (policy.IsFailure)
            return Result.Failure<CredentialVerificationResult, VerificationError>(policy.Error);
        var decoded = TwicCredentialEvidenceCodec.Decode(input.Request);
        if (decoded.IsFailure || decoded.Value.Generation != input.Profile.Generation)
            return Failure("twic.envelope-invalid");
        using var live = new TwicLiveSession(
            input.Session,
            input.Profile,
            input.MarsecLevel,
            authority,
            executor,
            clock,
            decoded.Value,
            input.PrivacyKey,
            input.Generation
        );
        return await VerifyCoreAsync(live, input, policy.Value).ConfigureAwait(false);
    }

    private async Task<Result<CredentialVerificationResult, VerificationError>> VerifyCoreAsync(
        TwicLiveSession live,
        TwicLiveVerificationInput input,
        GuidedFingerprintCaptureService.AcquisitionPolicy policy
    )
    {
        input.Token.ThrowIfCancellationRequested();
        if (!live.ValidContext(input.Request))
            return Failure("twic.live-context-unavailable");
        var current = live.Copy(
            input.Request,
            ImmutableArray<byte>.Empty,
            Maybe<HolderVerificationEvidence>.None
        );
        var preflight = await new TwicLivePreflight(authority, verifier)
            .CheckAsync(live, current, input.Token)
            .ConfigureAwait(false);
        if (preflight.HasValue)
            return preflight.Value;
        var key = live.AcquirePrivacyKey(input.Token);
        if (key.IsFailure)
            return Result.Failure<CredentialVerificationResult, VerificationError>(key.Error);
        var holder = await CaptureHolderAsync(live, current, input, policy).ConfigureAwait(false);
        if (holder.IsFailure)
            return Result.Failure<CredentialVerificationResult, VerificationError>(holder.Error);
        if (!live.IsCurrent)
            return Failure("twic.session-changed");
        return await CompleteAsync(
                live,
                live.Copy(current, ImmutableArray<byte>.Empty, holder.Value),
                input.Token
            )
            .ConfigureAwait(false);
    }

    private async Task<Result<HolderVerificationEvidence, VerificationError>> CaptureHolderAsync(
        TwicLiveSession live,
        CredentialVerificationRequest current,
        TwicLiveVerificationInput input,
        GuidedFingerprintCaptureService.AcquisitionPolicy policy
    )
    {
        var prepared = await fingerprints
            .PrepareTwicAsync(
                current,
                live.Profile,
                live.Session,
                live.PrivacyKey.Value,
                input.Token
            )
            .ConfigureAwait(false);
        if (prepared.IsFailure)
            return Result.Failure<HolderVerificationEvidence, VerificationError>(prepared.Error);
        return await new GuidedFingerprintCaptureService(fingerprints, devices, adapter, clock)
            .CaptureAsync(
                prepared.Value,
                current,
                live.Session,
                input.DeviceId,
                policy,
                input.Guide,
                input.Token
            )
            .ConfigureAwait(false);
    }

    private async Task<Result<CredentialVerificationResult, VerificationError>> CompleteAsync(
        TwicLiveSession live,
        CredentialVerificationRequest current,
        CancellationToken token
    )
    {
        if (live.Profile.RequireCardAuthentication)
        {
            var completed = live.Prove(current, token);
            if (completed.IsFailure)
                return Result.Failure<CredentialVerificationResult, VerificationError>(
                    completed.Error
                );
            current = completed.Value;
        }
        token.ThrowIfCancellationRequested();
        if (!live.IsCurrent)
            return Failure("twic.session-changed");
        current = live.Copy(current, current.CardProof, current.HolderEvidence);
        var result = await verifier
            .VerifyAsync(current, live.Profile, live.Evidence(current, true), token)
            .ConfigureAwait(false);
        return live.IsCurrent ? result : Failure("twic.session-changed");
    }

    private static Result<CredentialVerificationResult, VerificationError> Failure(string reason) =>
        Result.Failure<CredentialVerificationResult, VerificationError>(
            new(VerificationErrorCodes.EvidenceUnavailable, reason)
        );
}
