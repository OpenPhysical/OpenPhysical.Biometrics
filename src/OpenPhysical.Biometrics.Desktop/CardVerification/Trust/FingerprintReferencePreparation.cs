// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Inspection;
using Org.BouncyCastle.X509;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

/// <summary>Owns authenticated reference preparation and clears decrypted TWIC bytes before returning.</summary>
internal sealed class FingerprintReferencePreparation(
    FingerprintCeremonyService owner,
    TimeProvider clock,
    FingerprintReferenceAuthenticator authentication
)
{
    internal Task<Result<PreparedFingerprintReference, VerificationError>> PrepareAsync(
        CredentialVerificationRequest request,
        ILocalCardSession session,
        ImmutableArray<X509Certificate> contentSignerChain,
        CancellationToken token
    ) =>
        CaptureVerificationEdge.RunAsync(
            () => PreparePivAsync(request, session, contentSignerChain, token),
            "ceremony.reference-unavailable"
        );

    private Task<Result<PreparedFingerprintReference, VerificationError>> PreparePivAsync(
        CredentialVerificationRequest request,
        ILocalCardSession session,
        ImmutableArray<X509Certificate> contentSignerChain,
        CancellationToken token
    )
    {
        var read = PivFingerprintReferenceReader.Read(request, session, token);
        return read.IsFailure
            ? Task.FromResult(
                Result.Failure<PreparedFingerprintReference, VerificationError>(read.Error)
            )
            : PrepareReferenceAsync(
                new(
                    request,
                    session,
                    read.Value.Chuid,
                    read.Value.Fingerprint,
                    contentSignerChain,
                    false
                ),
                token
            );
    }

    internal Task<Result<PreparedFingerprintReference, VerificationError>> PrepareTwicAsync(
        CredentialVerificationRequest request,
        TwicAcceptanceProfile profile,
        ILocalCardSession session,
        TwicPrivacyKeyContainer privacyKey,
        CancellationToken token
    ) =>
        CaptureVerificationEdge.RunAsync(
            () => PrepareTwicCoreAsync(request, profile, session, privacyKey, token),
            "ceremony.reference-unavailable"
        );

    private async Task<
        Result<PreparedFingerprintReference, VerificationError>
    > PrepareTwicCoreAsync(
        CredentialVerificationRequest request,
        TwicAcceptanceProfile profile,
        ILocalCardSession session,
        TwicPrivacyKeyContainer privacyKey,
        CancellationToken token
    )
    {
        var read = TwicFingerprintReferenceReader.Read(request, profile, session, token);
        if (read.IsFailure)
            return read.Error;
        var plaintext = FingerprintReferenceCryptography.DecryptTwicReference(
            read.Value.Encrypted,
            privacyKey
        );
        if (plaintext.IsFailure)
            return PreparationFailure();
        try
        {
            return await PrepareReferenceAsync(
                    new(
                        request,
                        session,
                        read.Value.Chuid,
                        plaintext.Value,
                        read.Value.ContentSignerChain,
                        true
                    ),
                    token
                )
                .ConfigureAwait(false);
        }
        finally
        {
            FingerprintReferenceCryptography.Clear(plaintext.Value);
        }
    }

    private async Task<
        Result<PreparedFingerprintReference, VerificationError>
    > PrepareReferenceAsync(ReferenceOperation operation, CancellationToken token)
    {
        var authenticated = await authentication
            .AuthenticateAsync(
                operation.Request,
                operation.Chuid,
                operation.Fingerprint,
                operation.Chain,
                operation.Twic,
                token
            )
            .ConfigureAwait(false);
        if (authenticated.IsFailure)
            return authenticated.Error;
        return CreateOwnedReference(operation, authenticated.Value);
    }

    private Result<PreparedFingerprintReference, VerificationError> CreateOwnedReference(
        ReferenceOperation operation,
        FingerprintReferenceAuthenticator.AuthenticatedReference reference
    )
    {
        var now = clock.GetUtcNow();
        var validUntil = FreshUntil(operation.Request, reference, now);
        var selections = reference
            .Reference.Views.Where(view =>
                view.FingerPosition is >= 1 and <= 10 && !view.Minutiae.IsEmpty
            )
            .Select(view => new FingerprintReferenceSelection(view.FingerPosition, view.ViewNumber))
            .Distinct()
            .ToImmutableArray();
        if (
            selections.IsEmpty
            || validUntil.HasNoValue
            || operation.Session.EnsureCurrent().IsFailure
        )
            return PreparationFailure();
        return new PreparedFingerprintReference(
            owner,
            operation.Session,
            operation.Request,
            now,
            validUntil.Value,
            selections,
            operation.Chuid,
            operation.Fingerprint,
            operation.Chain,
            operation.Twic
        );
    }

    private static Maybe<DateTimeOffset> FreshUntil(
        CredentialVerificationRequest request,
        FingerprintReferenceAuthenticator.AuthenticatedReference reference,
        DateTimeOffset now
    )
    {
        var expiry =
            reference.Cbeff.Header.ValidityNotAfter < reference.Identity.ExpiresAt
                ? reference.Cbeff.Header.ValidityNotAfter
                : reference.Identity.ExpiresAt;
        var freshness = VerificationFreshnessEvaluator.Evaluate(
            request.Bundle,
            now,
            request.ClockUncertainty,
            expiry,
            reference.Identity.SignerEvidence.RequiredEvidence.AddRange(
                reference.Identity.AuthenticationEvidence.RequiredEvidence
            )
        );
        return freshness.Check.State == VerificationCheckState.Passed
            ? freshness.ValidUntil
            : Maybe<DateTimeOffset>.None;
    }

    private static Result<PreparedFingerprintReference, VerificationError> PreparationFailure() =>
        Result.Failure<PreparedFingerprintReference, VerificationError>(
            new(VerificationErrorCodes.EvidenceUnavailable, "ceremony.reference-unavailable")
        );

    private sealed record ReferenceOperation(
        CredentialVerificationRequest Request,
        ILocalCardSession Session,
        ImmutableArray<byte> Chuid,
        ImmutableArray<byte> Fingerprint,
        ImmutableArray<X509Certificate> Chain,
        bool Twic
    );
}
