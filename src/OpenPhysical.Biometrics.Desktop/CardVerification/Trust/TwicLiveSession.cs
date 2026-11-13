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

internal sealed class TwicLiveSession(
    ILocalCardSession session,
    TwicAcceptanceProfile profile,
    int marsecLevel,
    CredentialCeremonyService authority,
    PivApduExecutor executor,
    TimeProvider clock,
    TwicEnvelope envelope,
    Maybe<TwicPrivacyKeyContainer> privacyKey,
    long expectedGeneration
) : IDisposable
{
    private readonly long generation = expectedGeneration;
    private ImmutableArray<byte> ownedKey = ImmutableArray<byte>.Empty;
    internal ILocalCardSession Session => session;
    internal TwicAcceptanceProfile Profile => profile;
    internal TwicEnvelope Envelope { get; } = envelope;
    internal Maybe<TwicPrivacyKeyContainer> PrivacyKey { get; private set; } = privacyKey;
    internal bool IsCurrent =>
        session.Generation == generation && session.EnsureCurrent().IsSuccess;

    internal bool ValidContext(CredentialVerificationRequest request) =>
        IsCurrent
        && session.Card.Mode == ConnectionMode.Exclusive
        && marsecLevel is >= 1 and <= 3
        && request.Workflow == CredentialVerificationWorkflow.Twic
        && TwicAcceptanceService.AuthenticatedProfile(request.Bundle, profile);

    internal CredentialVerificationRequest Copy(
        CredentialVerificationRequest request,
        ImmutableArray<byte> proof,
        Maybe<HolderVerificationEvidence> holder
    ) =>
        new(
            clock.GetUtcNow(),
            request.ClockUncertainty,
            request.ProfileId,
            request.Workflow,
            request.CredentialSnapshot,
            request.CredentialSha256,
            proof,
            holder,
            request.Lifecycle,
            request.Bundle,
            request.Offline
        );

    internal CredentialCeremonyService Authority => authority;
    internal PivApduExecutor Executor => executor;

    internal Result<CredentialVerificationRequest, VerificationError> Prove(
        CredentialVerificationRequest value,
        CancellationToken token
    ) => TwicNativeCardProof.Execute(this, value, token);

    internal TwicAcceptanceEvidence Evidence(CredentialVerificationRequest value, bool decrypted)
    {
        var cardCheck = Check(
            profile.RequireCardAuthentication,
            value.CardProof.IsEmpty ? VerificationCheckState.Missing : VerificationCheckState.Passed
        );
        var privacyCheck = Check(
            true,
            decrypted ? VerificationCheckState.Passed : VerificationCheckState.Missing
        );
        return new(
            value.CredentialSha256,
            profile.ProfileId,
            profile.Version,
            profile.Generation,
            profile.Workflow,
            marsecLevel,
            value.EvaluatedAt,
            ImmutableArray<string>.Empty,
            cardCheck,
            privacyCheck,
            CredentialStatusState.Unknown,
            Maybe<StatusEvidenceDescriptor>.None,
            value.HolderEvidence,
            Maybe<string>.None
        );
    }

    internal UnitResult<VerificationError> AcquirePrivacyKey(CancellationToken token)
    {
        if (PrivacyKey.HasValue)
            return UnitResult.Success<VerificationError>();
        var read = TwicNativePrivacyKey.Read(this, token);
        if (read.IsFailure)
            return UnitResult.Failure(read.Error);
        ownedKey = read.Value.Key;
        PrivacyKey = read.Value;
        return UnitResult.Success<VerificationError>();
    }

    private static UnitResult<VerificationError> KeyFailure(string reason) =>
        UnitResult.Failure(
            new VerificationError(VerificationErrorCodes.EvidenceUnavailable, reason)
        );

    public void Dispose() => FingerprintReferenceCryptography.Clear(ownedKey);

    private static VerificationCheck Check(bool required, VerificationCheckState state) =>
        new(
            required,
            !required ? VerificationCheckState.NotRequired : state,
            !required || state == VerificationCheckState.Passed
                ? ImmutableArray<string>.Empty
                : ImmutableArray.Create("twic.ceremony-missing")
        );
}
