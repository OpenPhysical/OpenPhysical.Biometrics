namespace OpenPhysical.Biometrics.Core.Certificates;

using CSharpFunctionalExtensions;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;

/// <summary>
/// Interface for managing trusted certificates for CBEFF signature verification.
/// </summary>
public interface ITrustedCertificateStore
{
    /// <summary>
    /// Gets all trusted certificates from the store.
    /// </summary>
    /// <returns>A store of trusted certificates.</returns>
    Result<IStore<X509Certificate>> GetTrustedCertificates();

    /// <summary>
    /// Gets trusted certificates by issuer name.
    /// </summary>
    /// <param name="issuerName">The issuer distinguished name to search for.</param>
    /// <returns>A store of certificates matching the issuer.</returns>
    Result<IStore<X509Certificate>> GetCertificatesByIssuer(string issuerName);

    /// <summary>
    /// Adds a certificate to the trusted store.
    /// </summary>
    /// <param name="certificate">The certificate to add.</param>
    /// <returns>Success if the certificate was added, failure otherwise.</returns>
    Result AddCertificate(X509Certificate certificate);

    /// <summary>
    /// Removes a certificate from the trusted store.
    /// </summary>
    /// <param name="thumbprint">The thumbprint of the certificate to remove.</param>
    /// <returns>Success if the certificate was removed, failure otherwise.</returns>
    Result RemoveCertificate(string thumbprint);

    /// <summary>
    /// Checks if the store has been initialized with default certificates.
    /// </summary>
    /// <returns>True if the store is initialized, false otherwise.</returns>
    bool IsInitialized();

    /// <summary>
    /// Initializes the store with default ICAM test certificates.
    /// </summary>
    /// <returns>Success if initialization completed, failure otherwise.</returns>
    Result InitializeWithDefaults();

    /// <summary>
    /// Gets the path to the certificate store directory.
    /// </summary>
    /// <returns>The full path to the certificate store directory.</returns>
    string GetStorePath();
}
