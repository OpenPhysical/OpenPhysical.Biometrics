namespace OpenPhysical.Biometrics.Core.Domain;

using System;
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.X509;

/// <summary>
/// Immutable metadata record for X.509 certificates with multiple identifiers.
/// </summary>
public sealed record CertificateMetadata
{
    /// <summary>
    /// Gets the SHA-256 fingerprint of the certificate (unique identifier).
    /// </summary>
    public ImmutableArray<byte> Fingerprint { get; init; }

    /// <summary>
    /// Gets the Subject Key Identifier extension value, if present.
    /// </summary>
    public Maybe<ImmutableArray<byte>> SubjectKeyIdentifier { get; init; }

    /// <summary>
    /// Gets the Authority Key Identifier extension value, if present.
    /// </summary>
    public Maybe<ImmutableArray<byte>> AuthorityKeyIdentifier { get; init; }

    /// <summary>
    /// Gets the certificate serial number.
    /// </summary>
    public required BigInteger SerialNumber { get; init; }

    /// <summary>
    /// Gets the subject distinguished name.
    /// </summary>
    public required string SubjectDN { get; init; }

    /// <summary>
    /// Gets the issuer distinguished name.
    /// </summary>
    public required string IssuerDN { get; init; }

    /// <summary>
    /// Gets the certificate validity start date.
    /// </summary>
    public DateTime NotBefore { get; init; }

    /// <summary>
    /// Gets the certificate validity end date.
    /// </summary>
    public DateTime NotAfter { get; init; }

    /// <summary>
    /// Creates certificate metadata from an X.509 certificate.
    /// </summary>
    /// <param name="certificate">The certificate to extract metadata from.</param>
    /// <returns>A result containing the certificate metadata or an error.</returns>
    public static Result<CertificateMetadata> FromCertificate(X509Certificate certificate)
    {
        if (certificate == null)
            return Result.Failure<CertificateMetadata>("Certificate cannot be null");

        try
        {
            // Calculate SHA-256 fingerprint
            var certBytes = certificate.GetEncoded();
            var sha256 = new Org.BouncyCastle.Crypto.Digests.Sha256Digest();
            sha256.BlockUpdate(certBytes, 0, certBytes.Length);
            var fingerprint = new byte[sha256.GetDigestSize()];
            sha256.DoFinal(fingerprint, 0);

            // Extract Subject Key Identifier
            var skiBytes = certificate.GetExtensionValue(X509Extensions.SubjectKeyIdentifier);
            var subjectKeyId = Maybe<ImmutableArray<byte>>.None;
            if (skiBytes != null)
            {
                var ski = Org.BouncyCastle.Asn1.X509.SubjectKeyIdentifier.GetInstance(
                    Org.BouncyCastle.Asn1.Asn1OctetString.GetInstance(skiBytes).GetOctets()
                );
                subjectKeyId = Maybe.From(ski.GetKeyIdentifier().ToImmutableArray());
            }

            // Extract Authority Key Identifier
            var akiBytes = certificate.GetExtensionValue(X509Extensions.AuthorityKeyIdentifier);
            var authorityKeyId = Maybe<ImmutableArray<byte>>.None;
            if (akiBytes != null)
            {
                var aki = Org.BouncyCastle.Asn1.X509.AuthorityKeyIdentifier.GetInstance(
                    Org.BouncyCastle.Asn1.Asn1OctetString.GetInstance(akiBytes).GetOctets()
                );
                if (aki.GetKeyIdentifier() != null)
                {
                    authorityKeyId = Maybe.From(aki.GetKeyIdentifier().ToImmutableArray());
                }
            }

            var metadata = new CertificateMetadata
            {
                Fingerprint = fingerprint.ToImmutableArray(),
                SubjectKeyIdentifier = subjectKeyId,
                AuthorityKeyIdentifier = authorityKeyId,
                SerialNumber = certificate.SerialNumber,
                SubjectDN = certificate.SubjectDN.ToString(),
                IssuerDN = certificate.IssuerDN.ToString(),
                NotBefore = certificate.NotBefore,
                NotAfter = certificate.NotAfter,
            };

            return Result.Success(metadata);
        }
        catch (Exception ex)
        {
            return Result.Failure<CertificateMetadata>(
                $"Failed to extract certificate metadata: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Gets a hex string representation of the fingerprint.
    /// </summary>
    public string GetFingerprintHex() => Convert.ToHexString(Fingerprint.AsSpan());

    /// <summary>
    /// Gets a hex string representation of the Subject Key Identifier.
    /// </summary>
    public Maybe<string> GetSubjectKeyIdentifierHex() =>
        SubjectKeyIdentifier.Map(ski => Convert.ToHexString(ski.AsSpan()));

    /// <summary>
    /// Gets a composite key for serial number + issuer DN lookups.
    /// </summary>
    public string GetSerialIssuerKey() => $"{SerialNumber}|{IssuerDN}";
}
