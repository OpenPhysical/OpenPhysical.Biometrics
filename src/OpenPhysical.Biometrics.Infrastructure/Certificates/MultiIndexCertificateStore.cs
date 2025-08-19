namespace OpenPhysical.Biometrics.Infrastructure.Certificates;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Certificates;
using OpenPhysical.Biometrics.Core.Domain;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;

/// <summary>
/// Certificate store implementation with multiple indexes for flexible lookups.
/// </summary>
public sealed class MultiIndexCertificateStore : ITrustedCertificateStoreExtended
{
    private readonly ILogger<MultiIndexCertificateStore> _logger;
    private readonly Lazy<ITrustedCertificateProvider> _certificateProvider;
    
    // Primary storage - fingerprint to certificate
    private readonly ImmutableDictionary<string, X509Certificate> _certificatesByFingerprint;
    private readonly ImmutableDictionary<string, CertificateMetadata> _metadataByFingerprint;
    
    // Secondary indexes
    private readonly ImmutableDictionary<string, ImmutableHashSet<string>> _fingerprintsBySubjectKeyId;
    private readonly ImmutableDictionary<string, ImmutableHashSet<string>> _fingerprintsBySubjectDN;
    private readonly ImmutableDictionary<string, ImmutableHashSet<string>> _fingerprintsByIssuerDN;
    private readonly ImmutableDictionary<string, ImmutableHashSet<string>> _fingerprintsBySerialNumber;
    private readonly ImmutableDictionary<string, string> _fingerprintBySerialIssuer;

    /// <inheritdoc />
    public int CertificateCount => _certificatesByFingerprint.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiIndexCertificateStore"/> class.
    /// </summary>
    public MultiIndexCertificateStore(
        ILogger<MultiIndexCertificateStore> logger,
        Lazy<ITrustedCertificateProvider> certificateProvider)
        : this(logger, certificateProvider, 
            ImmutableDictionary<string, X509Certificate>.Empty,
            ImmutableDictionary<string, CertificateMetadata>.Empty,
            ImmutableDictionary<string, ImmutableHashSet<string>>.Empty,
            ImmutableDictionary<string, ImmutableHashSet<string>>.Empty,
            ImmutableDictionary<string, ImmutableHashSet<string>>.Empty,
            ImmutableDictionary<string, ImmutableHashSet<string>>.Empty,
            ImmutableDictionary<string, string>.Empty)
    {
    }

    private MultiIndexCertificateStore(
        ILogger<MultiIndexCertificateStore> logger,
        Lazy<ITrustedCertificateProvider> certificateProvider,
        ImmutableDictionary<string, X509Certificate> certificatesByFingerprint,
        ImmutableDictionary<string, CertificateMetadata> metadataByFingerprint,
        ImmutableDictionary<string, ImmutableHashSet<string>> fingerprintsBySubjectKeyId,
        ImmutableDictionary<string, ImmutableHashSet<string>> fingerprintsBySubjectDN,
        ImmutableDictionary<string, ImmutableHashSet<string>> fingerprintsByIssuerDN,
        ImmutableDictionary<string, ImmutableHashSet<string>> fingerprintsBySerialNumber,
        ImmutableDictionary<string, string> fingerprintBySerialIssuer)
    {
        _logger = logger;
        _certificateProvider = certificateProvider;
        _certificatesByFingerprint = certificatesByFingerprint;
        _metadataByFingerprint = metadataByFingerprint;
        _fingerprintsBySubjectKeyId = fingerprintsBySubjectKeyId;
        _fingerprintsBySubjectDN = fingerprintsBySubjectDN;
        _fingerprintsByIssuerDN = fingerprintsByIssuerDN;
        _fingerprintsBySerialNumber = fingerprintsBySerialNumber;
        _fingerprintBySerialIssuer = fingerprintBySerialIssuer;
    }

    /// <inheritdoc />
    public Result<IStore<X509Certificate>> GetTrustedCertificates()
    {
        try
        {
            var certificates = _certificatesByFingerprint.Values.ToList();
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
        return FindByIssuerDN(issuerName)
            .Map(certs => CollectionUtilities.CreateStore(certs.ToList()));
    }

    /// <inheritdoc />
    public Result AddCertificate(X509Certificate certificate)
    {
        return Result.Failure("MultiIndexCertificateStore is immutable. Use CreateWithCertificate to add certificates.");
    }

    /// <inheritdoc />
    public Result RemoveCertificate(string thumbprint)
    {
        return Result.Failure("MultiIndexCertificateStore is immutable. Use CreateWithoutCertificate to remove certificates.");
    }

    /// <inheritdoc />
    public bool IsInitialized()
    {
        return _certificatesByFingerprint.Count > 0;
    }

    /// <inheritdoc />
    public Result InitializeWithDefaults()
    {
        if (IsInitialized())
        {
            return Result.Failure("Store is already initialized");
        }

        return CreateWithDefaults()
            .Map(_ => Result.Success())
            .GetValueOrDefault(Result.Failure("Failed to initialize with defaults"));
    }

    /// <inheritdoc />
    public string GetStorePath()
    {
        return "[Multi-Index In-Memory Store]";
    }

    /// <inheritdoc />
    public Result<ImmutableArray<X509Certificate>> FindBySubjectKeyIdentifier(byte[] subjectKeyIdentifier)
    {
        try
        {
            var skiHex = Convert.ToHexString(subjectKeyIdentifier);
            if (_fingerprintsBySubjectKeyId.TryGetValue(skiHex, out var fingerprints))
            {
                var certificates = fingerprints
                    .Select(fp => _certificatesByFingerprint[fp])
                    .ToImmutableArray();
                return Result.Success(certificates);
            }
            return Result.Success(ImmutableArray<X509Certificate>.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find certificates by SKI");
            return Result.Failure<ImmutableArray<X509Certificate>>($"Failed to find certificates: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result<ImmutableArray<X509Certificate>> FindBySubjectDN(string subjectDN)
    {
        try
        {
            if (_fingerprintsBySubjectDN.TryGetValue(subjectDN, out var fingerprints))
            {
                var certificates = fingerprints
                    .Select(fp => _certificatesByFingerprint[fp])
                    .ToImmutableArray();
                return Result.Success(certificates);
            }
            return Result.Success(ImmutableArray<X509Certificate>.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find certificates by subject DN");
            return Result.Failure<ImmutableArray<X509Certificate>>($"Failed to find certificates: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result<ImmutableArray<X509Certificate>> FindByIssuerDN(string issuerDN)
    {
        try
        {
            if (_fingerprintsByIssuerDN.TryGetValue(issuerDN, out var fingerprints))
            {
                var certificates = fingerprints
                    .Select(fp => _certificatesByFingerprint[fp])
                    .ToImmutableArray();
                return Result.Success(certificates);
            }
            return Result.Success(ImmutableArray<X509Certificate>.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find certificates by issuer DN");
            return Result.Failure<ImmutableArray<X509Certificate>>($"Failed to find certificates: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result<ImmutableArray<X509Certificate>> FindBySerialNumber(BigInteger serialNumber)
    {
        try
        {
            var serialHex = serialNumber.ToString();
            if (_fingerprintsBySerialNumber.TryGetValue(serialHex, out var fingerprints))
            {
                var certificates = fingerprints
                    .Select(fp => _certificatesByFingerprint[fp])
                    .ToImmutableArray();
                return Result.Success(certificates);
            }
            return Result.Success(ImmutableArray<X509Certificate>.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find certificates by serial number");
            return Result.Failure<ImmutableArray<X509Certificate>>($"Failed to find certificates: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result<Maybe<X509Certificate>> FindBySerialNumberAndIssuer(BigInteger serialNumber, string issuerDN)
    {
        try
        {
            var key = $"{serialNumber}|{issuerDN}";
            if (_fingerprintBySerialIssuer.TryGetValue(key, out var fingerprint))
            {
                return Result.Success(Maybe.From(_certificatesByFingerprint[fingerprint]));
            }
            return Result.Success(Maybe<X509Certificate>.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find certificate by serial and issuer");
            return Result.Failure<Maybe<X509Certificate>>($"Failed to find certificate: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result<Maybe<X509Certificate>> FindByFingerprint(byte[] fingerprint)
    {
        try
        {
            var fingerprintHex = Convert.ToHexString(fingerprint);
            if (_certificatesByFingerprint.TryGetValue(fingerprintHex, out var certificate))
            {
                return Result.Success(Maybe.From(certificate));
            }
            return Result.Success(Maybe<X509Certificate>.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find certificate by fingerprint");
            return Result.Failure<Maybe<X509Certificate>>($"Failed to find certificate: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result<ImmutableArray<X509Certificate>> GetAllCertificates()
    {
        try
        {
            return Result.Success(_certificatesByFingerprint.Values.ToImmutableArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all certificates");
            return Result.Failure<ImmutableArray<X509Certificate>>($"Failed to get certificates: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result<ImmutableArray<CertificateMetadata>> GetAllMetadata()
    {
        try
        {
            return Result.Success(_metadataByFingerprint.Values.ToImmutableArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all metadata");
            return Result.Failure<ImmutableArray<CertificateMetadata>>($"Failed to get metadata: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result<ImmutableArray<X509Certificate>> FindWhere(Func<CertificateMetadata, bool> predicate)
    {
        try
        {
            var matchingFingerprints = _metadataByFingerprint
                .Where(kvp => predicate(kvp.Value))
                .Select(kvp => kvp.Key);

            var certificates = matchingFingerprints
                .Select(fp => _certificatesByFingerprint[fp])
                .ToImmutableArray();

            return Result.Success(certificates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find certificates with predicate");
            return Result.Failure<ImmutableArray<X509Certificate>>($"Failed to find certificates: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a new store with a certificate added.
    /// </summary>
    public Result<MultiIndexCertificateStore> CreateWithCertificate(X509Certificate certificate)
    {
        return CertificateMetadata.FromCertificate(certificate)
            .Bind(metadata =>
            {
                var fingerprintHex = metadata.GetFingerprintHex();
                
                // Check if already exists
                if (_certificatesByFingerprint.ContainsKey(fingerprintHex))
                {
                    _logger.LogInformation("Certificate already exists with fingerprint {Fingerprint}", fingerprintHex);
                    return Result.Success(this);
                }

                // Update primary storage
                var newCertificates = _certificatesByFingerprint.SetItem(fingerprintHex, certificate);
                var newMetadata = _metadataByFingerprint.SetItem(fingerprintHex, metadata);

                // Update indexes
                var newSkiIndex = UpdateMultiValueIndex(_fingerprintsBySubjectKeyId, 
                    metadata.GetSubjectKeyIdentifierHex().GetValueOrDefault(string.Empty), fingerprintHex);
                var newSubjectIndex = UpdateMultiValueIndex(_fingerprintsBySubjectDN, metadata.SubjectDN, fingerprintHex);
                var newIssuerIndex = UpdateMultiValueIndex(_fingerprintsByIssuerDN, metadata.IssuerDN, fingerprintHex);
                var newSerialIndex = UpdateMultiValueIndex(_fingerprintsBySerialNumber, metadata.SerialNumber.ToString(), fingerprintHex);
                var newSerialIssuerIndex = _fingerprintBySerialIssuer.SetItem(metadata.GetSerialIssuerKey(), fingerprintHex);

                _logger.LogInformation("Added certificate {Subject} with fingerprint {Fingerprint}", 
                    certificate.SubjectDN, fingerprintHex);

                return Result.Success(new MultiIndexCertificateStore(
                    _logger, _certificateProvider, newCertificates, newMetadata,
                    newSkiIndex, newSubjectIndex, newIssuerIndex, newSerialIndex, newSerialIssuerIndex));
            });
    }

    /// <summary>
    /// Creates a new store with multiple certificates added.
    /// </summary>
    public Result<MultiIndexCertificateStore> CreateWithCertificates(IEnumerable<X509Certificate> certificates)
    {
        var store = this;
        foreach (var cert in certificates)
        {
            var result = store.CreateWithCertificate(cert);
            if (result.IsFailure)
                return Result.Failure<MultiIndexCertificateStore>($"Failed to add certificate: {result.Error}");
            store = result.Value;
        }
        return Result.Success(store);
    }

    /// <summary>
    /// Creates a new store initialized with default certificates.
    /// </summary>
    public Result<MultiIndexCertificateStore> CreateWithDefaults()
    {
        var provider = _certificateProvider.Value;
        return provider.GetDefaultCertificates()
            .Bind(certStore =>
            {
                var certificates = new List<X509Certificate>();
                foreach (var cert in certStore.EnumerateMatches(null))
                {
                    certificates.Add(cert);
                }

                _logger.LogInformation("Loading {Count} default certificates", certificates.Count);
                return CreateWithCertificates(certificates);
            });
    }

    /// <summary>
    /// Calculates the SHA-256 fingerprint for a certificate.
    /// </summary>
    public byte[] CalculateFingerprint(X509Certificate certificate)
    {
        var sha256 = new Org.BouncyCastle.Crypto.Digests.Sha256Digest();
        var certBytes = certificate.GetEncoded();
        sha256.BlockUpdate(certBytes, 0, certBytes.Length);
        var fingerprint = new byte[sha256.GetDigestSize()];
        sha256.DoFinal(fingerprint, 0);
        return fingerprint;
    }

    private static ImmutableDictionary<string, ImmutableHashSet<string>> UpdateMultiValueIndex(
        ImmutableDictionary<string, ImmutableHashSet<string>> index,
        string key,
        string value)
    {
        if (string.IsNullOrEmpty(key))
            return index;

        var currentSet = index.GetValueOrDefault(key, ImmutableHashSet<string>.Empty);
        return index.SetItem(key, currentSet.Add(value));
    }
}