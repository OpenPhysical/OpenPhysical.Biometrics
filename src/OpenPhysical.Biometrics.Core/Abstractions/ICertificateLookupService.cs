namespace OpenPhysical.Biometrics.Core.Abstractions;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Domain;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.X509;

/// <summary>
/// Service for flexible certificate lookups with multiple search criteria.
/// </summary>
public interface ICertificateLookupService
{
    /// <summary>
    /// Finds certificates by Subject Key Identifier.
    /// </summary>
    /// <param name="subjectKeyIdentifier">The Subject Key Identifier bytes.</param>
    /// <returns>A result containing matching certificates.</returns>
    Result<ImmutableArray<X509Certificate>> FindBySubjectKeyIdentifier(byte[] subjectKeyIdentifier);

    /// <summary>
    /// Finds certificates by subject distinguished name.
    /// </summary>
    /// <param name="subjectDN">The subject DN to search for.</param>
    /// <returns>A result containing matching certificates.</returns>
    Result<ImmutableArray<X509Certificate>> FindBySubjectDN(string subjectDN);

    /// <summary>
    /// Finds certificates by issuer distinguished name.
    /// </summary>
    /// <param name="issuerDN">The issuer DN to search for.</param>
    /// <returns>A result containing matching certificates.</returns>
    Result<ImmutableArray<X509Certificate>> FindByIssuerDN(string issuerDN);

    /// <summary>
    /// Finds certificates by serial number.
    /// </summary>
    /// <param name="serialNumber">The certificate serial number.</param>
    /// <returns>A result containing matching certificates (may be multiple if duplicates exist).</returns>
    Result<ImmutableArray<X509Certificate>> FindBySerialNumber(BigInteger serialNumber);

    /// <summary>
    /// Finds certificates by serial number and issuer DN combination.
    /// </summary>
    /// <param name="serialNumber">The certificate serial number.</param>
    /// <param name="issuerDN">The issuer DN.</param>
    /// <returns>A result containing the matching certificate (should be unique).</returns>
    Result<Maybe<X509Certificate>> FindBySerialNumberAndIssuer(BigInteger serialNumber, string issuerDN);

    /// <summary>
    /// Finds a certificate by its SHA-256 fingerprint.
    /// </summary>
    /// <param name="fingerprint">The certificate fingerprint.</param>
    /// <returns>A result containing the certificate if found.</returns>
    Result<Maybe<X509Certificate>> FindByFingerprint(byte[] fingerprint);

    /// <summary>
    /// Gets all stored certificates.
    /// </summary>
    /// <returns>A result containing all certificates.</returns>
    Result<ImmutableArray<X509Certificate>> GetAllCertificates();

    /// <summary>
    /// Gets metadata for all stored certificates.
    /// </summary>
    /// <returns>A result containing certificate metadata.</returns>
    Result<ImmutableArray<CertificateMetadata>> GetAllMetadata();

    /// <summary>
    /// Finds certificates that match a predicate.
    /// </summary>
    /// <param name="predicate">The predicate to match certificates against.</param>
    /// <returns>A result containing matching certificates.</returns>
    Result<ImmutableArray<X509Certificate>> FindWhere(System.Func<CertificateMetadata, bool> predicate);

    /// <summary>
    /// Calculates the SHA-256 fingerprint for a certificate.
    /// </summary>
    /// <param name="certificate">The certificate to calculate fingerprint for.</param>
    /// <returns>The SHA-256 fingerprint bytes.</returns>
    byte[] CalculateFingerprint(X509Certificate certificate);
}