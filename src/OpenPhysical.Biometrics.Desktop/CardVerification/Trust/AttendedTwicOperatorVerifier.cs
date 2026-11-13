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

internal sealed class AttendedTwicOperatorVerifier(CredentialCeremonyService authority)
{
    internal async Task<
        Result<FingerprintComparisonCeremony.AttendedOperatorAuthorization, VerificationError>
    > VerifyAsync(
        CredentialVerificationRequest request,
        TwicAcceptanceProfile profile,
        CredentialVerificationRequest operatorRequest,
        CancellationToken cancellationToken
    )
    {
        if (!ValidContext(request, profile, operatorRequest))
            return Failure();
        var path = CredentialEvidenceCodec.Decode(operatorRequest);
        if (path.IsFailure || path.Value.IsDefaultOrEmpty)
            return Failure();
        var hash = FingerprintReferenceCryptography.Hash(path.Value[0].GetEncoded());
        if (!Permitted(request, hash))
            return Failure();
        var verified = await new CredentialVerificationService(authority)
            .VerifyAsync(operatorRequest, cancellationToken)
            .ConfigureAwait(false);
        if (
            verified.IsFailure
            || verified.Value.Decision != CredentialVerificationDecision.Accepted
        )
            return Failure();
        return new FingerprintComparisonCeremony.AttendedOperatorAuthorization(
            hash,
            verified.Value.ValidUntil.Value
        );
    }

    private static bool ValidContext(
        CredentialVerificationRequest request,
        TwicAcceptanceProfile profile,
        CredentialVerificationRequest actor
    ) =>
        profile.PermitAttendedAlternative
        && profile.ProfileId == request.ProfileId
        && TwicAcceptanceService.AuthenticatedProfile(request.Bundle, profile)
        && actor.ProfileId == CredentialEvidenceCodec.PinAuthenticationProfile
        && actor.Bundle.Policy.RequireTrustedIssuer
        && actor.Bundle.Policy.RequireStatusEvidence
        && actor.Bundle.ManifestSha256 == request.Bundle.ManifestSha256
        && actor.CredentialSha256 != request.CredentialSha256;

    private static bool Permitted(CredentialVerificationRequest request, string hash)
    {
        var permissions = request
            .Bundle.Manifest.Artifacts.Where(value =>
                value.Kind == BundleArtifactKind.VerificationProfile
                && value.ProfileId == "openphysical.twic-attended-operators"
                && value.ProfileVersion == 1
                && value.MediaType == "application/json"
            )
            .ToArray();
        if (permissions.Length != 1)
            return false;
        var permitted = VerificationManifestCodec.Decode(
            request.Bundle.AuthenticatedArtifacts[permissions[0].ArtifactId],
            root =>
            {
                if (root.GetProperty("schemaVersion").GetInt32() != 1)
                    return false;
                var operators = root.GetProperty("operatorCertificateSha256");
                return operators.GetArrayLength() is > 0 and <= 256
                    && operators.EnumerateArray().Any(value => value.GetString() == hash);
            }
        );
        return permitted.IsSuccess && permitted.Value;
    }

    private static Result<
        FingerprintComparisonCeremony.AttendedOperatorAuthorization,
        VerificationError
    > Failure() =>
        Result.Failure<
            FingerprintComparisonCeremony.AttendedOperatorAuthorization,
            VerificationError
        >(new(VerificationErrorCodes.EvidenceUnavailable, "ceremony.fingerprint"));
}
