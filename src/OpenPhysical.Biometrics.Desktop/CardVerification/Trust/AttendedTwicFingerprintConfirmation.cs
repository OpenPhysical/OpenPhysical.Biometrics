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

internal sealed class AttendedTwicFingerprintConfirmation(
    CredentialCeremonyService authority,
    FingerprintComparisonCeremony ceremony
)
{
    internal async Task<Result<HolderVerificationEvidence, VerificationError>> ConfirmAsync(
        CredentialVerificationRequest request,
        TwicAcceptanceProfile profile,
        TwicPrivacyKeyContainer privacyKey,
        CredentialVerificationRequest operatorRequest,
        CancellationToken cancellationToken
    )
    {
        var approved = await new AttendedTwicOperatorVerifier(authority)
            .VerifyAsync(request, profile, operatorRequest, cancellationToken)
            .ConfigureAwait(false);
        if (approved.IsFailure)
            return Result.Failure<HolderVerificationEvidence, VerificationError>(approved.Error);
        var operatorHash = approved.Value.CertificateSha256;
        var verifiedExpiry = approved.Value.ExpiresAt;
        return await ceremony
            .ProcessTwicReferenceAsync(
                request,
                privacyKey,
                Maybe<FingerprintCapture>.None,
                0,
                new FingerprintComparisonCeremony.AttendedOperatorAuthorization(
                    operatorHash,
                    verifiedExpiry
                ),
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
