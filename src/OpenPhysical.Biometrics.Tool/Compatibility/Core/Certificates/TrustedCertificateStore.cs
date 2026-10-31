namespace OpenPhysical.Biometrics.Core.Certificates;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;

/// <summary>
/// File-based implementation of trusted certificate store that manages certificates on disk.
/// </summary>
public sealed class TrustedCertificateStore : ITrustedCertificateStore
{
    private readonly ILogger<TrustedCertificateStore> _logger;
    private readonly TrustedCertificateStoreOptions _options;
    private readonly Lazy<ITrustedCertificateProvider> _certificateProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrustedCertificateStore"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The store options.</param>
    /// <param name="certificateProvider">The certificate provider for default certificates.</param>
    public TrustedCertificateStore(
        ILogger<TrustedCertificateStore> logger,
        IOptions<TrustedCertificateStoreOptions> options,
        Lazy<ITrustedCertificateProvider> certificateProvider
    )
    {
        _logger = logger;
        _options = options.Value;
        _certificateProvider = certificateProvider;
    }

    /// <inheritdoc />
    public Result<IStore<X509Certificate>> GetTrustedCertificates()
    {
        try
        {
            if (!IsInitialized() && _options.AutoInitializeWithDefaults)
            {
                var initResult = InitializeWithDefaults();
                if (initResult.IsFailure)
                {
                    return Result.Failure<IStore<X509Certificate>>(
                        $"Failed to initialize certificate store: {initResult.Error}"
                    );
                }
            }

            var certificates = new List<X509Certificate>();

            if (!Directory.Exists(_options.StorePath))
            {
                return Result.Success(CollectionUtilities.CreateStore(certificates));
            }

            var certFiles = Directory
                .GetFiles(_options.StorePath, "*.crt")
                .Concat(Directory.GetFiles(_options.StorePath, "*.cer"))
                .Concat(Directory.GetFiles(_options.StorePath, "*.pem"));

            foreach (var certFile in certFiles)
            {
                try
                {
                    var certBytes = File.ReadAllBytes(certFile);
                    var cert = new X509Certificate(certBytes);
                    certificates.Add(cert);
                    _logger.LogDebug(
                        new EventId(693143, "TrustedCertificateStore"),
                        "TrustedCertificateStore stage completed."
                    );
                }
                catch (Exception)
                {
                    _logger.LogWarning(
                        new EventId(615169, "TrustedCertificateStore"),
                        "TrustedCertificateStore requires review."
                    );
                }
            }

            return Result.Success(CollectionUtilities.CreateStore(certificates));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(679003, "TrustedCertificateStore"),
                "TrustedCertificateStore failed."
            );
            return Result.Failure<IStore<X509Certificate>>(
                $"Failed to get trusted certificates: {ex.Message}"
            );
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
                    if (
                        cert
                            .IssuerDN.ToString()
                            .Contains(issuerName, StringComparison.OrdinalIgnoreCase)
                    )
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
            EnsureStoreDirectoryExists();

            // Use certificate's serial number for filename since BouncyCastle doesn't have thumbprint
            var fileName = $"{certificate.SerialNumber}.crt";
            var filePath = Path.Combine(_options.StorePath, fileName);

            if (File.Exists(filePath))
            {
                _logger.LogInformation(
                    new EventId(850285, "TrustedCertificateStore"),
                    "TrustedCertificateStore stage completed."
                );
                return Result.Success();
            }

            var certBytes = certificate.GetEncoded();
            File.WriteAllBytes(filePath, certBytes);

            _logger.LogInformation(
                new EventId(768270, "TrustedCertificateStore"),
                "TrustedCertificateStore stage completed."
            );
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(878136, "TrustedCertificateStore"),
                "TrustedCertificateStore failed."
            );
            return Result.Failure($"Failed to add certificate: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result RemoveCertificate(string thumbprint)
    {
        try
        {
            // Since BouncyCastle doesn't have thumbprint, we'll treat this as serial number
            var fileName = $"{thumbprint}.crt";
            var filePath = Path.Combine(_options.StorePath, fileName);

            if (!File.Exists(filePath))
            {
                return Result.Failure(
                    $"Certificate with identifier {thumbprint} not found in store"
                );
            }

            File.Delete(filePath);
            _logger.LogInformation(
                new EventId(737762, "TrustedCertificateStore"),
                "TrustedCertificateStore stage completed."
            );
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(892276, "TrustedCertificateStore"),
                "TrustedCertificateStore failed."
            );
            return Result.Failure($"Failed to remove certificate: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public bool IsInitialized()
    {
        return Directory.Exists(_options.StorePath)
            && Directory.GetFiles(_options.StorePath, "*.crt").Length > 0;
    }

    /// <inheritdoc />
    public Result InitializeWithDefaults()
    {
        try
        {
            EnsureStoreDirectoryExists();

            if (IsInitialized())
            {
                _logger.LogDebug("Certificate store already initialized");
                return Result.Success();
            }

            _logger.LogInformation(
                "Initializing trusted certificate store with default ICAM certificates"
            );

            var defaultCertificatesResult = _certificateProvider.Value.GetDefaultCertificates();
            if (defaultCertificatesResult.IsFailure)
            {
                return Result.Failure(
                    $"Failed to get default certificates: {defaultCertificatesResult.Error}"
                );
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
                    _logger.LogWarning(
                        new EventId(842308, "TrustedCertificateStore"),
                        "TrustedCertificateStore requires review."
                    );
                }
            }

            _logger.LogInformation(
                "Initialized certificate store with {Count} default certificates",
                addedCount
            );
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(663400, "TrustedCertificateStore"),
                "TrustedCertificateStore failed."
            );
            return Result.Failure($"Failed to initialize certificate store: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public string GetStorePath()
    {
        return _options.StorePath;
    }

    private void EnsureStoreDirectoryExists()
    {
        if (_options.CreateDirectoryIfNotExists && !Directory.Exists(_options.StorePath))
        {
            Directory.CreateDirectory(_options.StorePath);
            _logger.LogDebug(
                new EventId(663518, "TrustedCertificateStore"),
                "TrustedCertificateStore stage completed."
            );
        }
    }
}
