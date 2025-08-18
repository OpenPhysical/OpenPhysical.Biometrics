namespace OpenPhysical.Biometrics.Core.Certificates;

using CSharpFunctionalExtensions;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;

/// <summary>
/// Interface for providing default trusted certificates.
/// </summary>
public interface ITrustedCertificateProvider
{
    /// <summary>
    /// Gets the default trusted certificates for ICAM test cards.
    /// </summary>
    /// <returns>A store of default trusted certificates.</returns>
    Result<IStore<X509Certificate>> GetDefaultCertificates();

    /// <summary>
    /// Gets certificates by name pattern.
    /// </summary>
    /// <param name="namePattern">The certificate name pattern to search for.</param>
    /// <returns>A store of matching certificates.</returns>
    Result<IStore<X509Certificate>> GetCertificatesByName(string namePattern);
}