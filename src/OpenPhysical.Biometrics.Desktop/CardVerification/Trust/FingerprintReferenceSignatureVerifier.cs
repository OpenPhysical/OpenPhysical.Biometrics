// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Threading;
using System.Threading.Tasks;
using OpenPhysical.Biometrics.Piv;
using OpenPhysical.Middleware.Cbeff.Signatures;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Validation;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.X509;
using BiometricValidation = OpenPhysical.Biometrics.Piv.ValidationStatus;

namespace OpenPhysical.Middleware.Infrastructure.Trust;

/// <summary>Uses the already authenticated signer and unchanged bounded biometric algorithms.</summary>
internal static class FingerprintReferenceSignatureVerifier
{
    internal static CmsBiometricSignatureVerifier Create(
        SignedCredentialReference identity,
        bool twic
    ) =>
        new(
            identity.Signer,
            new SuppliedSignerTrust(identity.Signer, identity.SignerEvidence),
            new[]
            {
                NistObjectIdentifiers.IdSha256.Id,
                NistObjectIdentifiers.IdSha384.Id,
                NistObjectIdentifiers.IdSha512.Id,
            },
            new[]
            {
                PkcsObjectIdentifiers.Sha256WithRsaEncryption.Id,
                PkcsObjectIdentifiers.Sha384WithRsaEncryption.Id,
                PkcsObjectIdentifiers.Sha512WithRsaEncryption.Id,
                PkcsObjectIdentifiers.IdRsassaPss.Id,
                X9ObjectIdentifiers.ECDsaWithSha256.Id,
                X9ObjectIdentifiers.ECDsaWithSha384.Id,
                X9ObjectIdentifiers.ECDsaWithSha512.Id,
            },
            false,
            allowTwicIdentityAlias: twic,
            allowLegacyAbsentUuid: twic && identity.Identity.CardUuid == Guid.Empty
        );

    private sealed class SuppliedSignerTrust(
        X509Certificate expected,
        VerificationCertificatePathEvidence evidence
    ) : IBiometricCertificateTrustEvaluator
    {
        /// <inheritdoc />
        public Task<BiometricCertificateTrust> EvaluateAsync(
            X509Certificate certificate,
            CancellationToken token
        )
        {
            token.ThrowIfCancellationRequested();
            if (!expected.GetEncoded().AsSpan().SequenceEqual(certificate.GetEncoded()))
                return Task.FromResult(
                    new BiometricCertificateTrust(
                        BiometricValidation.Failed,
                        BiometricValidation.Failed
                    )
                );
            return Task.FromResult(
                new BiometricCertificateTrust(
                    evidence.Trust.State == VerificationCheckState.Passed
                        ? BiometricValidation.Passed
                        : BiometricValidation.NotEvaluated,
                    evidence.Status.State == VerificationCheckState.Passed
                        ? BiometricValidation.Passed
                        : BiometricValidation.NotEvaluated
                )
            );
        }
    }
}
