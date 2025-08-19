namespace OpenPhysical.Biometrics.Infrastructure.Certificates;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Certificates;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;

/// <summary>
/// In-memory implementation of trusted certificate store for testing.
/// This implementation is immutable - operations that modify certificates return new instances.
/// </summary>
public sealed class InMemoryTrustedCertificateStore : ITrustedCertificateStore
{
    private readonly ILogger<InMemoryTrustedCertificateStore> _logger;
    private readonly Lazy<ITrustedCertificateProvider> _certificateProvider;
    private readonly ImmutableDictionary<string, X509Certificate> _certificates;
    private readonly bool _autoInitializeWithDefaults;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryTrustedCertificateStore"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="certificateProvider">The certificate provider for default certificates.</param>
    /// <param name="autoInitializeWithDefaults">Whether to auto-initialize with default certificates.</param>
    public InMemoryTrustedCertificateStore(
        ILogger<InMemoryTrustedCertificateStore> logger,
        Lazy<ITrustedCertificateProvider> certificateProvider,
        bool autoInitializeWithDefaults = true)
        : this(logger, certificateProvider, ImmutableDictionary<string, X509Certificate>.Empty, autoInitializeWithDefaults)
    {
    }

    private InMemoryTrustedCertificateStore(
        ILogger<InMemoryTrustedCertificateStore> logger,
        Lazy<ITrustedCertificateProvider> certificateProvider,
        ImmutableDictionary<string, X509Certificate> certificates,
        bool autoInitializeWithDefaults)
    {
        _logger = logger;
        _certificateProvider = certificateProvider;
        _certificates = certificates;
        _autoInitializeWithDefaults = autoInitializeWithDefaults;
    }

    /// <inheritdoc />
    public Result<IStore<X509Certificate>> GetTrustedCertificates()
    {
        try
        {
            if (!IsInitialized() && _autoInitializeWithDefaults)
            {
                return Result.Failure<IStore<X509Certificate>>(
                    "InMemoryTrustedCertificateStore is immutable and not initialized. " +
                    "Use CreateWithDefaults() to create an initialized instance.");
            }

            var certificates = _certificates.Values.ToList();
            return Result.Success(CollectionUtilities.CreateStore(certificates));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get trusted certificates");
            return Result.Failure<IStore<X509Certificate>>($"Failed to get trusted certificates: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result<IStore<X509Certificate>> GetCertificatesByIssuer(string issuerName)
    {
        return GetTrustedCertificates()
            .Map(certificateStore =>
            {
                var filtered = new List<X509Certificate>();
                foreach (var cert in certificateStore.EnumerateMatches(null))
                {
                    if (cert.IssuerDN.ToString().Contains(issuerName, StringComparison.OrdinalIgnoreCase))
                    {
                        filtered.Add(cert);
                    }
                }
                return CollectionUtilities.CreateStore(filtered);
            });
    }

    /// <inheritdoc />
    public Result AddCertificate(X509Certificate certificate)
    {
        return Result.Failure("InMemoryTrustedCertificateStore is immutable. Use CreateWithCertificate to add certificates.");
    }

    /// <summary>
    /// Creates a new immutable certificate store with the specified certificate added.
    /// </summary>
    /// <param name="certificate">The certificate to add.</param>
    /// <returns>A new certificate store instance with the certificate added.</returns>
    public Result<InMemoryTrustedCertificateStore> CreateWithCertificate(X509Certificate certificate)
    {
        try
        {
            var identifier = certificate.SerialNumber.ToString();
            var newCertificates = _certificates.SetItem(identifier, certificate);
            
            _logger.LogInformation("Created new certificate store with certificate {Subject}", certificate.SubjectDN);
            return Result.Success(new InMemoryTrustedCertificateStore(_logger, _certificateProvider, newCertificates, _autoInitializeWithDefaults));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create certificate store with certificate {Subject}", certificate.SubjectDN);
            return Result.Failure<InMemoryTrustedCertificateStore>($"Failed to add certificate: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result RemoveCertificate(string thumbprint)
    {
        return Result.Failure("InMemoryTrustedCertificateStore is immutable. Use CreateWithoutCertificate to remove certificates.");
    }

    /// <summary>
    /// Creates a new immutable certificate store with the specified certificate removed.
    /// </summary>
    /// <param name="thumbprint">The certificate identifier to remove.</param>
    /// <returns>A new certificate store instance with the certificate removed.</returns>
    public Result<InMemoryTrustedCertificateStore> CreateWithoutCertificate(string thumbprint)
    {
        try
        {
            if (_certificates.ContainsKey(thumbprint))
            {
                var newCertificates = _certificates.Remove(thumbprint);
                var removedCert = _certificates[thumbprint];
                
                _logger.LogInformation("Created new certificate store without certificate {Subject}", removedCert.SubjectDN);
                return Result.Success(new InMemoryTrustedCertificateStore(_logger, _certificateProvider, newCertificates, _autoInitializeWithDefaults));
            }
            
            return Result.Failure<InMemoryTrustedCertificateStore>($"Certificate with identifier {thumbprint} not found in store");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create certificate store without certificate {Identifier}", thumbprint);
            return Result.Failure<InMemoryTrustedCertificateStore>($"Failed to remove certificate: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public bool IsInitialized()
    {
        return _certificates.Count > 0;
    }

    /// <inheritdoc />
    public Result InitializeWithDefaults()
    {
        return Result.Failure("InMemoryTrustedCertificateStore is immutable. Use CreateWithDefaults to initialize with default certificates.");
    }

    /// <summary>
    /// Creates a new immutable certificate store initialized with default certificates.
    /// </summary>
    /// <returns>A new certificate store instance with default certificates loaded.</returns>
    public Result<InMemoryTrustedCertificateStore> CreateWithDefaults()
    {
        try
        {
            _logger.LogInformation("Creating in-memory certificate store with default ICAM certificates");

            var defaultCertificatesResult = _certificateProvider.Value.GetDefaultCertificates();
            if (defaultCertificatesResult.IsFailure)
            {
                return Result.Failure<InMemoryTrustedCertificateStore>($"Failed to get default certificates: {defaultCertificatesResult.Error}");
            }

            var defaultCertificates = defaultCertificatesResult.Value;
            var certificateDict = ImmutableDictionary<string, X509Certificate>.Empty;
            var addedCount = 0;

            foreach (var cert in defaultCertificates.EnumerateMatches(null))
            {
                try
                {
                    var identifier = cert.SerialNumber.ToString();
                    certificateDict = certificateDict.SetItem(identifier, cert);
                    addedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add default certificate {Subject}: {Error}", cert.SubjectDN, ex.Message);
                }
            }

            _logger.LogInformation("Created in-memory certificate store with {Count} default certificates", addedCount);
            return Result.Success(new InMemoryTrustedCertificateStore(_logger, _certificateProvider, certificateDict, false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create certificate store with defaults");
            return Result.Failure<InMemoryTrustedCertificateStore>($"Failed to create certificate store with defaults: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public string GetStorePath()
    {
        return "[In-Memory Store]";
    }

    /// <summary>
    /// Creates a new empty certificate store.
    /// </summary>
    /// <returns>A new empty certificate store instance.</returns>
    public Result<InMemoryTrustedCertificateStore> CreateEmpty()
    {
        _logger.LogDebug("Created new empty certificate store");
        return Result.Success(new InMemoryTrustedCertificateStore(_logger, _certificateProvider, ImmutableDictionary<string, X509Certificate>.Empty, false));
    }

    /// <summary>
    /// Gets the current count of certificates in the store.
    /// </summary>
    public int Count => _certificates.Count;
}