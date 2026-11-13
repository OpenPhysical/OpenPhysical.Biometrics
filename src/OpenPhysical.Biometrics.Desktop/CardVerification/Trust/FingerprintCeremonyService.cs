// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.Piv;
using OpenPhysical.CardEdge.Credential;
using OpenPhysical.CardEdge.Twic.Crypto;
using OpenPhysical.Middleware.Cbeff.Signatures;
using OpenPhysical.Middleware.Domain.Cryptography.Oids;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Piv.Codec;
using OpenPhysical.Middleware.Domain.SmartCard;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure.CredentialCache;
using OpenPhysical.Middleware.Infrastructure.Inspection;
using OpenPhysical.Middleware.Infrastructure.Twic;
using OpenPhysical.Middleware.Infrastructure.Validation;
using OpenPhysical.Middleware.Inspection;
using OpenPhysical.Middleware.Piv.Codec;
using OpenPhysical.Middleware.Validation;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.X509;
using BiometricValidation = OpenPhysical.Biometrics.Piv.ValidationStatus;
using CbeffCodec = OpenPhysical.Biometrics.Formats.Cbeff.CbeffCodec;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

/// <summary>Authenticates the reference before comparing a fresh captured fingerprint.</summary>
public sealed class FingerprintCeremonyService
{
    private readonly TimeProvider clock;
    private readonly FingerprintComparisonCeremony matching;
    private readonly FingerprintReferencePreparation preparation;

    /// <summary>Uses the configured biometric engine and the verifier's own receipt authority.</summary>
    /// <param name="authority">Shared card and holder receipt authority.</param>
    /// <param name="comparison">Real extraction/comparison adapter.</param>
    /// <param name="clock">Host clock for capture and reference freshness.</param>
    public FingerprintCeremonyService(
        CredentialCeremonyService authority,
        FingerprintComparisonService comparison,
        TimeProvider clock
    )
    {
        this.clock = clock;
        var authentication = new FingerprintReferenceAuthenticator(clock);
        matching = new FingerprintComparisonCeremony(authority, comparison, clock, authentication);
        preparation = new FingerprintReferencePreparation(this, clock, authentication);
    }

    /// <summary>Verifies signed CHUID/CBEFF identity and policy, then compares the fresh captured finger.</summary>
    /// <param name="request">Credential snapshot and authenticated verification policy.</param>
    /// <param name="signedChuid">Exact signed CHUID from this credential.</param>
    /// <param name="signedFingerprint">Exact unwrapped CBEFF fingerprint object.</param>
    /// <param name="contentSignerChain">Leaf-first path for the common CHUID/biometric signer.</param>
    /// <param name="capture">Capture supplied through the authorized workstation acquisition path.</param>
    /// <param name="viewNumber">Explicit reference view for the captured finger.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>An authority-local holder receipt only after every required check and actual match succeeds.</returns>
    public async Task<Result<HolderVerificationEvidence, VerificationError>> CompareAsync(
        CredentialVerificationRequest request,
        ImmutableArray<byte> signedChuid,
        ImmutableArray<byte> signedFingerprint,
        ImmutableArray<X509Certificate> contentSignerChain,
        FingerprintCapture capture,
        byte viewNumber,
        CancellationToken cancellationToken
    )
    {
        if (request.Workflow == CredentialVerificationWorkflow.Twic)
            return Failure();
        return await matching
            .CompareCoreAsync(
                request,
                signedChuid,
                signedFingerprint,
                contentSignerChain,
                capture,
                viewNumber,
                false,
                Maybe<FingerprintComparisonCeremony.AttendedOperatorAuthorization>.None,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>Authenticates this retained card's reference before presenting enrolled finger choices.</summary>
    /// <param name="request">Authenticated credential and workflow context.</param>
    /// <param name="session">The original exclusive card session.</param>
    /// <param name="contentSignerChain">Exact leaf-first content signer path.</param>
    /// <param name="cancellationToken">Cancellation for this operation.</param>
    /// <returns>The bounded operation result or a typed failure.</returns>
    public Task<Result<PreparedFingerprintReference, VerificationError>> PrepareAsync(
        CredentialVerificationRequest request,
        ILocalCardSession session,
        ImmutableArray<X509Certificate> contentSignerChain,
        CancellationToken cancellationToken
    ) => preparation.PrepareAsync(request, session, contentSignerChain, cancellationToken);

    /// <summary>Authenticates enrolled TWIC choices after matching the live encrypted objects on the retained lease.</summary>
    /// <remarks>The separately supplied privacy key is never retained. Physical card authentication remains a separate profile requirement.</remarks>
    /// <param name="request">Authenticated credential and workflow context.</param>
    /// <param name="profile">Exact authenticated TWIC workflow profile.</param>
    /// <param name="session">The original exclusive card session.</param>
    /// <param name="privacyKey">Caller-owned TWIC privacy key.</param>
    /// <param name="cancellationToken">Cancellation for this operation.</param>
    /// <returns>The bounded operation result or a typed failure.</returns>
    public Task<Result<PreparedFingerprintReference, VerificationError>> PrepareTwicAsync(
        CredentialVerificationRequest request,
        TwicAcceptanceProfile profile,
        ILocalCardSession session,
        TwicPrivacyKeyContainer privacyKey,
        CancellationToken cancellationToken
    ) => preparation.PrepareTwicAsync(request, profile, session, privacyKey, cancellationToken);

    /// <summary>Decrypts the envelope-bound TWIC reference before authenticating and comparing it.</summary>
    /// <param name="request">TWIC envelope and authenticated bundle.</param>
    /// <param name="privacyKey">Separately acquired TPK; never serialized into evidence or receipts.</param>
    /// <param name="capture">Authorized live acquisition.</param>
    /// <param name="viewNumber">Selected enrolled finger view.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>A holder receipt also attesting actual reference decryption.</returns>
    public Task<Result<HolderVerificationEvidence, VerificationError>> CompareTwicAsync(
        CredentialVerificationRequest request,
        TwicPrivacyKeyContainer privacyKey,
        FingerprintCapture capture,
        byte viewNumber,
        CancellationToken cancellationToken
    ) =>
        matching.ProcessTwicReferenceAsync(
            request,
            privacyKey,
            capture,
            viewNumber,
            Maybe<FingerprintComparisonCeremony.AttendedOperatorAuthorization>.None,
            cancellationToken
        );

    /// <summary>Confirms an authenticated no-finger/unusable-finger exception using an authorized operator's fresh PIV PIN proof.</summary>
    /// <param name="request">Subject TWIC request.</param>
    /// <param name="profile">Exact server-signed workflow permitting attended substitution.</param>
    /// <param name="privacyKey">Separately acquired subject TPK.</param>
    /// <param name="operatorRequest">Operator PIN-profile request with actual authority card/holder receipts.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Subject-bound attended receipt only after actual operator and exception authentication.</returns>
    public Task<Result<HolderVerificationEvidence, VerificationError>> ConfirmTwicExceptionAsync(
        CredentialVerificationRequest request,
        TwicAcceptanceProfile profile,
        TwicPrivacyKeyContainer privacyKey,
        CredentialVerificationRequest operatorRequest,
        CancellationToken cancellationToken
    ) =>
        matching.ConfirmTwicExceptionAsync(
            request,
            profile,
            privacyKey,
            operatorRequest,
            cancellationToken
        );

    /// <summary>Consumes a prepared reference only for its original live session and signed-policy context.</summary>
    /// <param name="reference">Single-use reference bound to that session and policy.</param>
    /// <param name="request">Authenticated credential and workflow context.</param>
    /// <param name="session">The original exclusive card session.</param>
    /// <param name="capture">Authorized live fingerprint acquisition.</param>
    /// <param name="viewNumber">Selected enrolled view number.</param>
    /// <param name="cancellationToken">Cancellation for this operation.</param>
    /// <returns>The bounded operation result or a typed failure.</returns>
    public async Task<Result<HolderVerificationEvidence, VerificationError>> ComparePreparedAsync(
        PreparedFingerprintReference reference,
        CredentialVerificationRequest request,
        ILocalCardSession session,
        FingerprintCapture capture,
        byte viewNumber,
        CancellationToken cancellationToken
    )
    {
        if (reference is null || request is null || session is null || capture is null)
            return Failure();
        if (
            capture.CapturedAt < reference.PreparedAt
            || !reference.Selections.Contains(
                new FingerprintReferenceSelection((byte)capture.Finger, viewNumber)
            )
            || !reference.Consume(this, session, request, clock.GetUtcNow())
        )
            return Failure();
        try
        {
            var compared = await reference
                .CompareAsync(matching, request, capture, viewNumber, cancellationToken)
                .ConfigureAwait(false);
            return !reference.IsDisposed && session.EnsureCurrent().IsSuccess
                ? compared
                : Failure();
        }
        finally
        {
            reference.Dispose();
        }
    }

    private static Result<HolderVerificationEvidence, VerificationError> Failure(
        VerificationErrorCodes code = VerificationErrorCodes.EvidenceUnavailable
    ) =>
        Result.Failure<HolderVerificationEvidence, VerificationError>(
            new VerificationError(code, "ceremony.fingerprint")
        );
}
