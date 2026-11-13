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

internal sealed class FingerprintComparisonCeremony(
    CredentialCeremonyService authority,
    FingerprintComparisonService comparison,
    TimeProvider clock,
    FingerprintReferenceAuthenticator authentication
)
{
    internal Task<Result<HolderVerificationEvidence, VerificationError>> ConfirmTwicExceptionAsync(
        CredentialVerificationRequest request,
        TwicAcceptanceProfile profile,
        TwicPrivacyKeyContainer privacyKey,
        CredentialVerificationRequest operatorRequest,
        CancellationToken token
    ) =>
        new AttendedTwicFingerprintConfirmation(authority, this).ConfirmAsync(
            request,
            profile,
            privacyKey,
            operatorRequest,
            token
        );

    internal async Task<
        Result<HolderVerificationEvidence, VerificationError>
    > ProcessTwicReferenceAsync(
        CredentialVerificationRequest request,
        TwicPrivacyKeyContainer privacyKey,
        Maybe<FingerprintCapture> capture,
        byte viewNumber,
        Maybe<AttendedOperatorAuthorization> attendedOperator,
        CancellationToken cancellationToken
    )
    {
        if (request is null || privacyKey is null)
            return Failure();
        if (
            request.Workflow != CredentialVerificationWorkflow.Twic
            || !privacyKey.IsAesEcb
            || privacyKey.Key.Length != 16
        )
            return Failure();
        var decoded = TwicCredentialEvidenceCodec.Decode(request);
        if (decoded.IsFailure || decoded.Value.EncryptedFingerprint.IsDefaultOrEmpty)
            return Failure();
        return await CompareDecryptedAsync(
                request,
                privacyKey,
                decoded.Value,
                capture,
                viewNumber,
                attendedOperator,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private Task<Result<HolderVerificationEvidence, VerificationError>> CompareDecryptedAsync(
        CredentialVerificationRequest request,
        TwicPrivacyKeyContainer privacyKey,
        TwicEnvelope envelope,
        Maybe<FingerprintCapture> capture,
        byte viewNumber,
        Maybe<AttendedOperatorAuthorization> attendedOperator,
        CancellationToken cancellationToken
    ) =>
        CaptureVerificationEdge.RunAsync(
            () =>
                UseDecryptedAsync(
                    new(
                        request,
                        privacyKey,
                        envelope,
                        capture,
                        viewNumber,
                        attendedOperator,
                        cancellationToken
                    )
                ),
            "ceremony.fingerprint"
        );

    private async Task<Result<HolderVerificationEvidence, VerificationError>> UseDecryptedAsync(
        TwicFingerprintComparisonInput input
    )
    {
        var plaintext = FingerprintReferenceCryptography.DecryptTwicReference(
            input.Envelope.EncryptedFingerprint,
            input.PrivacyKey
        );
        if (plaintext.IsFailure)
            return Failure();
        try
        {
            return await CompareAsync(
                    new(
                        input.Request,
                        input.Envelope.SignedChuid,
                        plaintext.Value,
                        input.Envelope.ContentSignerChain,
                        input.Capture,
                        input.ViewNumber,
                        true,
                        input.AttendedOperator,
                        input.Token
                    )
                )
                .ConfigureAwait(false);
        }
        finally
        {
            FingerprintReferenceCryptography.Clear(plaintext.Value);
        }
    }

    internal Task<Result<HolderVerificationEvidence, VerificationError>> CompareCoreAsync(
        CredentialVerificationRequest request,
        ImmutableArray<byte> signedChuid,
        ImmutableArray<byte> signedFingerprint,
        ImmutableArray<X509Certificate> contentSignerChain,
        Maybe<FingerprintCapture> capture,
        byte viewNumber,
        bool twic,
        Maybe<AttendedOperatorAuthorization> attendedOperator,
        CancellationToken cancellationToken
    ) =>
        CompareAsync(
            new(
                request,
                signedChuid,
                signedFingerprint,
                contentSignerChain,
                capture,
                viewNumber,
                twic,
                attendedOperator,
                cancellationToken
            )
        );

    private Task<Result<HolderVerificationEvidence, VerificationError>> CompareAsync(
        FingerprintComparisonInput input
    )
    {
        if (
            input.Request is null
            || input.Capture.HasNoValue && (!input.Twic || input.AttendedOperator.HasNoValue)
        )
            return Task.FromResult(Failure());
        if (input.Token.IsCancellationRequested)
            return Task.FromResult(Failure(VerificationErrorCodes.Cancelled));
        if (!input.Bounded)
            return Task.FromResult(Failure());
        return CaptureVerificationEdge.RunAsync(
            () => MatchAuthenticatedAsync(input),
            "ceremony.fingerprint"
        );
    }

    private async Task<
        Result<HolderVerificationEvidence, VerificationError>
    > MatchAuthenticatedAsync(FingerprintComparisonInput input)
    {
        var verified = await authentication
            .AuthenticateAsync(
                input.Request,
                input.SignedChuid,
                input.SignedFingerprint,
                input.ContentSignerChain,
                input.Twic,
                input.Token
            )
            .ConfigureAwait(false);
        if (verified.IsFailure)
            return Result.Failure<HolderVerificationEvidence, VerificationError>(verified.Error);
        return new FingerprintMatchEvaluator(authority, comparison, clock).Compare(
            input,
            verified.Value
        );
    }

    private static Result<HolderVerificationEvidence, VerificationError> Failure(
        VerificationErrorCodes code = VerificationErrorCodes.EvidenceUnavailable
    ) =>
        Result.Failure<HolderVerificationEvidence, VerificationError>(
            new VerificationError(code, "ceremony.fingerprint")
        );

    internal sealed record AttendedOperatorAuthorization(
        string CertificateSha256,
        DateTimeOffset ExpiresAt
    );
}
