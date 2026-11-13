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

internal sealed class FingerprintMatchEvaluator(
    CredentialCeremonyService authority,
    FingerprintComparisonService comparison,
    TimeProvider clock
)
{
    internal Result<HolderVerificationEvidence, VerificationError> Compare(
        FingerprintComparisonInput input,
        FingerprintReferenceAuthenticator.AuthenticatedReference prepared
    )
    {
        var now = clock.GetUtcNow();
        if (!CaptureCurrent(input.Capture, prepared.Profile.MaximumCaptureAge, now))
            return Failure();
        var expires = ReferenceExpiry(input, prepared);
        var identity = prepared.Identity;
        var freshness = VerificationFreshnessEvaluator.Evaluate(
            input.Request.Bundle,
            now,
            input.Request.ClockUncertainty,
            expires,
            identity.SignerEvidence.RequiredEvidence.AddRange(
                identity.AuthenticationEvidence.RequiredEvidence
            )
        );
        if (
            freshness.Check.State != VerificationCheckState.Passed
            || freshness.ValidUntil.HasNoValue
        )
            return Failure();
        input.Token.ThrowIfCancellationRequested();
        return input.Capture.HasNoValue
            ? Attended(input, prepared, freshness.ValidUntil.Value)
            : Match(input, prepared, freshness.ValidUntil.Value);
    }

    private static bool CaptureCurrent(
        Maybe<FingerprintCapture> capture,
        TimeSpan maximumAge,
        DateTimeOffset now
    ) =>
        capture.HasNoValue
        || capture.Value.Impression == FingerprintImpression.PlainLiveScan
            && capture.Value.CapturedAt.Offset == TimeSpan.Zero
            && capture.Value.CapturedAt <= now
            && now - capture.Value.CapturedAt <= maximumAge;

    private static DateTimeOffset ReferenceExpiry(
        FingerprintComparisonInput input,
        FingerprintReferenceAuthenticator.AuthenticatedReference prepared
    )
    {
        var expiry = prepared.Cbeff.Header.ValidityNotAfter;
        if (prepared.Identity.ExpiresAt < expiry)
            expiry = prepared.Identity.ExpiresAt;
        if (
            input.Capture.HasValue
            && input.Capture.Value.CapturedAt + prepared.Profile.MaximumCaptureAge < expiry
        )
            expiry = input.Capture.Value.CapturedAt + prepared.Profile.MaximumCaptureAge;
        if (input.AttendedOperator.HasValue && input.AttendedOperator.Value.ExpiresAt < expiry)
            expiry = input.AttendedOperator.Value.ExpiresAt;
        return expiry;
    }

    private Result<HolderVerificationEvidence, VerificationError> Attended(
        FingerprintComparisonInput input,
        FingerprintReferenceAuthenticator.AuthenticatedReference prepared,
        DateTimeOffset expiry
    )
    {
        var exception =
            prepared.Reference.Views.All(view => view.Minutiae.IsEmpty && view.Quality == 20)
            && prepared.Cbeff.Header.Quality is -2 or -1 or 0;
        if (!exception || input.AttendedOperator.HasNoValue)
            return Failure();
        var reference = FingerprintReferenceCryptography.Hash(
            System.Text.Encoding.UTF8.GetBytes(
                FingerprintReferenceCryptography.Hash(input.SignedFingerprint.ToArray())
                    + input.AttendedOperator.Value.CertificateSha256
            )
        );
        return Record(input, prepared, HolderVerificationMethod.Attended, reference, expiry, true);
    }

    private Result<HolderVerificationEvidence, VerificationError> Match(
        FingerprintComparisonInput input,
        FingerprintReferenceAuthenticator.AuthenticatedReference prepared,
        DateTimeOffset expiry
    )
    {
        var policy = FingerprintMatchingPolicy.Create(
            prepared.Profile.Identifier,
            prepared.Profile.MinimumScore
        );
        if (policy.IsFailure)
            return Failure();
        var reference = input.Twic ? ProjectTwic(prepared.Reference) : prepared.Reference;
        if (input.Twic && !SelectedTwicReference(reference, input))
            return Failure();
        var match = comparison.Compare(
            input.Capture.Value,
            reference,
            input.Capture.Value.Finger,
            input.ViewNumber,
            policy.Value
        );
        if (match.IsFailure)
            return Failure();
        if (!match.Value.Accepted)
            return Failure(VerificationErrorCodes.HolderMismatch);
        input.Token.ThrowIfCancellationRequested();
        return Record(
            input,
            prepared,
            HolderVerificationMethod.Biometric,
            FingerprintReferenceCryptography.Hash(input.SignedFingerprint.ToArray()),
            expiry,
            input.Twic
        );
    }

    private static bool SelectedTwicReference(
        FingerprintRecord reference,
        FingerprintComparisonInput input
    )
    {
        var selected = reference.Views.SingleOrDefault(view =>
            view.FingerPosition == (byte)input.Capture.Value.Finger
            && view.ViewNumber == input.ViewNumber
        );
        return selected is not null && !selected.Minutiae.IsEmpty;
    }

    private Result<HolderVerificationEvidence, VerificationError> Record(
        FingerprintComparisonInput input,
        FingerprintReferenceAuthenticator.AuthenticatedReference prepared,
        HolderVerificationMethod method,
        string reference,
        DateTimeOffset expiry,
        bool decrypted
    ) =>
        authority.RecordHolder(
            input.Request,
            FingerprintReferenceCryptography.Hash(
                prepared.Identity.AuthenticationPath[0].GetEncoded()
            ),
            method,
            reference,
            expiry,
            decrypted
        );

    private static FingerprintRecord ProjectTwic(FingerprintRecord reference) =>
        reference with
        {
            Views = reference
                .Views.Select(view =>
                    view with
                    {
                        Minutiae = view
                            .Minutiae.Where(point =>
                                point.X < reference.Width && point.Y < reference.Height
                            )
                            .ToImmutableArray(),
                        ExtendedBlocks = ImmutableArray<FingerprintExtendedBlock>.Empty,
                    }
                )
                .ToImmutableArray(),
        };

    private static Result<HolderVerificationEvidence, VerificationError> Failure(
        VerificationErrorCodes code = VerificationErrorCodes.EvidenceUnavailable
    ) =>
        Result.Failure<HolderVerificationEvidence, VerificationError>(
            new VerificationError(code, "ceremony.fingerprint")
        );
}
