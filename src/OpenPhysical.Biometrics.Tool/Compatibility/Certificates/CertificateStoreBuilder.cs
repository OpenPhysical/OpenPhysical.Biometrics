namespace OpenPhysical.Biometrics.Infrastructure.Certificates;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenPhysical.Biometrics.Core.Certificates;
using Org.BouncyCastle.X509;

/// <summary>
/// Builder for creating certificate stores for testing.
/// </summary>
public class CertificateStoreBuilder
{
    private readonly List<X509Certificate> _certificates = new();
    private readonly ILogger<InMemoryTrustedCertificateStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateStoreBuilder"/> class.
    /// </summary>
    public CertificateStoreBuilder()
        : this(NullLogger<InMemoryTrustedCertificateStore>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateStoreBuilder"/> class with a logger.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    public CertificateStoreBuilder(ILogger<InMemoryTrustedCertificateStore> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds the test root CA certificate.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public CertificateStoreBuilder WithTestRootCA()
    {
        _certificates.Add(TestCertificates.RootCA);
        return this;
    }

    /// <summary>
    /// Adds the test intermediate CA certificate.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public CertificateStoreBuilder WithTestIntermediateCA()
    {
        _certificates.Add(TestCertificates.IntermediateCA);
        return this;
    }

    /// <summary>
    /// Adds the test content signer certificate.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public CertificateStoreBuilder WithTestContentSigner()
    {
        _certificates.Add(TestCertificates.ContentSigner);
        return this;
    }

    /// <summary>
    /// Adds all test certificates.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public CertificateStoreBuilder WithAllTestCertificates()
    {
        return WithTestRootCA().WithTestIntermediateCA().WithTestContentSigner();
    }

    /// <summary>
    /// Adds a specific certificate.
    /// </summary>
    /// <param name="certificate">The certificate to add.</param>
    /// <returns>The builder for chaining.</returns>
    public CertificateStoreBuilder WithCertificate(X509Certificate certificate)
    {
        _certificates.Add(certificate);
        return this;
    }

    /// <summary>
    /// Adds multiple certificates.
    /// </summary>
    /// <param name="certificates">The certificates to add.</param>
    /// <returns>The builder for chaining.</returns>
    public CertificateStoreBuilder WithCertificates(IEnumerable<X509Certificate> certificates)
    {
        _certificates.AddRange(certificates);
        return this;
    }

    /// <summary>
    /// Builds an in-memory certificate store.
    /// </summary>
    /// <returns>The built certificate store.</returns>
    public ITrustedCertificateStore Build()
    {
        var provider = new StaticCertificateProvider(_certificates.ToImmutableArray());
        var emptyStore = new InMemoryTrustedCertificateStore(
            _logger,
            new System.Lazy<ITrustedCertificateProvider>(() => provider),
            autoInitializeWithDefaults: false
        );

        // Pre-populate the store with certificates using immutable pattern
        var currentStore = emptyStore;
        foreach (var cert in _certificates)
        {
            var addResult = currentStore.CreateWithCertificate(cert);
            if (addResult.IsSuccess)
            {
                currentStore = addResult.Value;
            }
            else
            {
                _logger.LogWarning(
                    new EventId(771225, "CertificateStoreBuilder"),
                    "CertificateStoreBuilder requires review."
                );
            }
        }

        return currentStore;
    }

    /// <summary>
    /// Simple certificate provider that returns a fixed set of certificates.
    /// </summary>
    private class StaticCertificateProvider : ITrustedCertificateProvider
    {
        private readonly ImmutableArray<X509Certificate> _certificates;

        public StaticCertificateProvider(ImmutableArray<X509Certificate> certificates)
        {
            _certificates = certificates;
        }

        public CSharpFunctionalExtensions.Result<Org.BouncyCastle.Utilities.Collections.IStore<X509Certificate>> GetDefaultCertificates()
        {
            return CSharpFunctionalExtensions.Result.Success(
                Org.BouncyCastle.Utilities.Collections.CollectionUtilities.CreateStore(
                    _certificates
                )
            );
        }

        public CSharpFunctionalExtensions.Result<Org.BouncyCastle.Utilities.Collections.IStore<X509Certificate>> GetCertificatesByName(
            string namePattern
        )
        {
            var filtered = _certificates
                .Where(cert =>
                    cert.SubjectDN.ToString()
                        .Contains(namePattern, System.StringComparison.OrdinalIgnoreCase)
                    || cert.IssuerDN.ToString()
                        .Contains(namePattern, System.StringComparison.OrdinalIgnoreCase)
                )
                .ToList();

            return CSharpFunctionalExtensions.Result.Success(
                Org.BouncyCastle.Utilities.Collections.CollectionUtilities.CreateStore(filtered)
            );
        }
    }
}
