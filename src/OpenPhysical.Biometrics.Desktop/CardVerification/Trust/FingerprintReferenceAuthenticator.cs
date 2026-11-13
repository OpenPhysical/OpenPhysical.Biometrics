// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.Piv;
using OpenPhysical.Middleware.Domain.Trust;
using Org.BouncyCastle.X509;
using BiometricValidation = OpenPhysical.Biometrics.Piv.ValidationStatus;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

/// <summary>Authenticates the signed identity and biometric reference under the original signed matching policy.</summary>
internal sealed class FingerprintReferenceAuthenticator(TimeProvider clock)
{
    internal Task<Result<AuthenticatedReference, VerificationError>> AuthenticateAsync(
        CredentialVerificationRequest request,
        ImmutableArray<byte> signedChuid,
        ImmutableArray<byte> signedFingerprint,
        ImmutableArray<X509Certificate> contentSignerChain,
        bool twic,
        CancellationToken token
    )
    {
        if (
            signedChuid.IsDefaultOrEmpty
            || signedChuid.Length > 65536
            || signedFingerprint.IsDefaultOrEmpty
            || signedFingerprint.Length > 65536
            || contentSignerChain.IsDefaultOrEmpty
            || contentSignerChain.Length is < 2 or > 10
        )
            return Task.FromResult(ReferenceFailure());
        return CaptureVerificationEdge.RunAsync(
            () =>
                AuthenticateCoreAsync(
                    new(request, signedChuid, signedFingerprint, contentSignerChain, twic),
                    token
                ),
            "ceremony.fingerprint-reference"
        );
    }

    private async Task<Result<AuthenticatedReference, VerificationError>> AuthenticateCoreAsync(
        AuthenticationOperation operation,
        CancellationToken token
    )
    {
        if (token.IsCancellationRequested)
            return ReferenceFailure(VerificationErrorCodes.Cancelled);
        var now = clock.GetUtcNow();
        var profile = FingerprintMatchingPolicyReader.Read(operation.Request);
        if (profile.IsFailure)
            return ReferenceFailure();
        var identity = await SignedCredentialReferenceVerifier
            .VerifyAsync(
                operation.Request,
                operation.Chuid,
                operation.Chain,
                operation.Twic,
                now,
                token
            )
            .ConfigureAwait(false);
        if (identity.IsFailure)
            return ReferenceFailure();
        var cbeff = CbeffCodec.Parse(operation.Fingerprint.AsSpan());
        if (cbeff.IsFailure || !ReferenceIsValid(cbeff.Value.Record, operation.Twic, now))
            return ReferenceFailure();
        return await VerifyBiometricAsync(
                profile.Value,
                cbeff.Value.Record,
                identity.Value,
                operation.Twic,
                token
            )
            .ConfigureAwait(false);
    }

    private static bool ReferenceIsValid(CbeffRecord cbeff, bool twic, DateTimeOffset now) =>
        twic
            ? cbeff.Header.FormatOwner == 0x001b
                && cbeff.Header.FormatType == 0x0201
                && cbeff.Header.BiometricType == 8
                && now >= cbeff.Header.ValidityNotBefore
                && now < cbeff.Header.ValidityNotAfter
            : PivBiometricProfile.Validate(cbeff, now).IsEmpty;

    private static async Task<
        Result<AuthenticatedReference, VerificationError>
    > VerifyBiometricAsync(
        MatchingProfile profile,
        CbeffRecord cbeff,
        SignedCredentialReference identity,
        bool twic,
        CancellationToken token
    )
    {
        var reference = Incits378Codec.Parse(
            cbeff.BiometricData.AsSpan(),
            new OpenPhysical.Biometrics.Formats.InspectionOptions(),
            twic
                ? FingerprintParsePolicy.PreserveOutOfBoundsMinutiae
                : FingerprintParsePolicy.Strict
        );
        if (reference.IsFailure)
            return ReferenceFailure();
        var verifier = FingerprintReferenceSignatureVerifier.Create(identity, twic);
        var report = await BiometricSignatureWorkflow
            .VerifyAsync(cbeff, identity.Identity, verifier, token)
            .ConfigureAwait(false);
        if (
            report.IsFailure
            || report.Value.IdentityBinding != BiometricValidation.Passed
            || report.Value.SignatureFindings.Signature != BiometricValidation.Passed
            || report.Value.SignatureFindings.Envelope != BiometricValidation.Passed
        )
            return ReferenceFailure();
        return new AuthenticatedReference(profile, reference.Value.Record, cbeff, identity);
    }

    private static Result<AuthenticatedReference, VerificationError> ReferenceFailure(
        VerificationErrorCodes code = VerificationErrorCodes.EvidenceUnavailable
    ) =>
        Result.Failure<AuthenticatedReference, VerificationError>(
            new(code, "ceremony.fingerprint-reference")
        );

    internal sealed record AuthenticatedReference(
        MatchingProfile Profile,
        FingerprintRecord Reference,
        CbeffRecord Cbeff,
        SignedCredentialReference Identity
    );

    internal sealed record MatchingProfile(
        string Identifier,
        int MinimumScore,
        TimeSpan MaximumCaptureAge
    );

    private sealed record AuthenticationOperation(
        CredentialVerificationRequest Request,
        ImmutableArray<byte> Chuid,
        ImmutableArray<byte> Fingerprint,
        ImmutableArray<X509Certificate> Chain,
        bool Twic
    );
}
