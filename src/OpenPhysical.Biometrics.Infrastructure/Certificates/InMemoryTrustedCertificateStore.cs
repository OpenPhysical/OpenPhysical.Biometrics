namespace OpenPhysical.Biometrics.Infrastructure.Certificates;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Certificates;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;

/// <summary>
/// In-memory implementation of trusted certificate store for testing.
/// </summary>
public sealed class InMemoryTrustedCertificateStore : ITrustedCertificateStore
{
    private readonly ILogger<InMemoryTrustedCertificateStore> _logger;
    private readonly Lazy<ITrustedCertificateProvider> _certificateProvider;
    private readonly ConcurrentDictionary<string, X509Certificate> _certificates;
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
    {
        _logger = logger;
        _certificateProvider = certificateProvider;
        _certificates = new ConcurrentDictionary<string, X509Certificate>();
        _autoInitializeWithDefaults = autoInitializeWithDefaults;
    }

    /// <inheritdoc />
    public Result<IStore<X509Certificate>> GetTrustedCertificates()
    {
        try
        {
            if (!IsInitialized() && _autoInitializeWithDefaults)
            {
                var initResult = InitializeWithDefaults();
                if (initResult.IsFailure)
                {
                    return Result.Failure<IStore<X509Certificate>>($"Failed to initialize certificate store: {initResult.Error}");
                }
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
        try
        {
            var identifier = certificate.SerialNumber.ToString();
            _certificates.AddOrUpdate(identifier, certificate, (key, existing) => certificate);
            
            _logger.LogInformation("Added certificate {Subject} to in-memory store", certificate.SubjectDN);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add certificate {Subject} to store", certificate.SubjectDN);
            return Result.Failure($"Failed to add certificate: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result RemoveCertificate(string thumbprint)
    {
        try
        {
            if (_certificates.TryRemove(thumbprint, out var removedCert))
            {
                _logger.LogInformation("Removed certificate {Subject} from in-memory store", removedCert.SubjectDN);
                return Result.Success();
            }
            
            return Result.Failure($"Certificate with identifier {thumbprint} not found in store");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove certificate {Identifier} from store", thumbprint);
            return Result.Failure($"Failed to remove certificate: {ex.Message}");
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
        try
        {
            if (IsInitialized())
            {
                _logger.LogDebug("In-memory certificate store already initialized");
                return Result.Success();
            }

            _logger.LogInformation("Initializing in-memory certificate store with default ICAM certificates");

            var defaultCertificatesResult = _certificateProvider.Value.GetDefaultCertificates();
            if (defaultCertificatesResult.IsFailure)
            {
                return Result.Failure($"Failed to get default certificates: {defaultCertificatesResult.Error}");
            }

            var defaultCertificates = defaultCertificatesResult.Value;
            var addedCount = 0;

            foreach (var cert in defaultCertificates.EnumerateMatches(null))
            {
                var addResult = AddCertificate(cert);
                if (addResult.IsSuccess)
                {
                    addedCount++;
                }
                else
                {
                    _logger.LogWarning("Failed to add default certificate {Subject}: {Error}", cert.SubjectDN, addResult.Error);
                }
            }

            _logger.LogInformation("Initialized in-memory certificate store with {Count} default certificates", addedCount);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize in-memory certificate store");
            return Result.Failure($"Failed to initialize certificate store: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public string GetStorePath()
    {
        return "[In-Memory Store]";
    }

    /// <summary>
    /// Clears all certificates from the in-memory store.
    /// </summary>
    public void Clear()
    {
        _certificates.Clear();
        _logger.LogDebug("Cleared all certificates from in-memory store");
    }

    /// <summary>
    /// Gets the current count of certificates in the store.
    /// </summary>
    public int Count => _certificates.Count;
}