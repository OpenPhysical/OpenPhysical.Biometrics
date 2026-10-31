namespace OpenPhysical.Biometrics.Core.Certificates;

using Abstractions;

/// <summary>
/// Extended certificate store interface that combines trusted store functionality with flexible lookups.
/// </summary>
public interface ITrustedCertificateStoreExtended
    : ITrustedCertificateStore,
        ICertificateLookupService
{
    /// <summary>
    /// Gets the total count of unique certificates in the store.
    /// </summary>
    int CertificateCount { get; }
}
