namespace OpenPhysical.Biometrics.Infrastructure.Tests.Certificates;

using System;
using System.IO;
using System.Linq;
using CSharpFunctionalExtensions;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using OpenPhysical.Biometrics.Core.Certificates;
using OpenPhysical.Biometrics.Infrastructure.Certificates;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;

[TestFixture]
public class TrustedCertificateStoreIntegrationTests
{
    private string _tempDirectory = null!;
    private TrustedCertificateStore _store = null!;
    private TrustedCertificateStoreOptions _options = null!;
    private ILogger<TrustedCertificateStore> _logger = null!;
    private Lazy<ITrustedCertificateProvider> _certificateProvider = null!;

    [SetUp]
    public void SetUp()
    {
        // Create temporary directory for test certificates
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        _logger = new LoggerFactory().CreateLogger<TrustedCertificateStore>();
        _options = new TrustedCertificateStoreOptions
        {
            StorePath = _tempDirectory,
            AutoInitializeWithDefaults = false,
            CreateDirectoryIfNotExists = true
        };

        // Create a simple provider that returns test certificates
        var provider = new SimpleTestCertificateProvider();
        _certificateProvider = new Lazy<ITrustedCertificateProvider>(() => provider);

        _store = new TrustedCertificateStore(_logger, Options.Create(_options), _certificateProvider);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public void InitializeWithDefaults_ShouldCreateCertificateFiles()
    {
        var result = _store.InitializeWithDefaults();

        result.IsSuccess.Should().BeTrue();
        _store.IsInitialized().Should().BeTrue();

        // Verify certificate files were created
        var certFiles = Directory.GetFiles(_tempDirectory, "*.crt");
        certFiles.Should().HaveCount(3);
    }

    [Test]
    public void GetTrustedCertificates_AfterInitialization_ShouldReturnStoredCertificates()
    {
        _store.InitializeWithDefaults();

        var result = _store.GetTrustedCertificates();

        result.IsSuccess.Should().BeTrue();
        var certificates = result.Value.EnumerateMatches(null).ToList();
        certificates.Should().HaveCount(3);
    }

    [Test]
    public void AddCertificate_ShouldPersistToFile()
    {
        // Start with empty store
        var originalCount = Directory.GetFiles(_tempDirectory, "*.crt").Length;
        originalCount.Should().Be(0);

        // Add a test certificate
        var testCert = TestCertificates.RootCA;
        var result = _store.AddCertificate(testCert);

        result.IsSuccess.Should().BeTrue();
        
        // File should be created
        var newCount = Directory.GetFiles(_tempDirectory, "*.crt").Length;
        newCount.Should().Be(1);
        
        // Verify file exists with expected name (serial number as filename)
        var expectedFile = Path.Combine(_tempDirectory, "1.crt");
        File.Exists(expectedFile).Should().BeTrue();
    }

    [Test]
    public void RemoveCertificate_ShouldDeleteFile()
    {
        _store.InitializeWithDefaults();
        
        // Get a certificate to remove
        var certsResult = _store.GetTrustedCertificates();
        var testCert = certsResult.Value.EnumerateMatches(null).First();
        var serialNumber = testCert.SerialNumber.ToString();
        
        var originalCount = Directory.GetFiles(_tempDirectory, "*.crt").Length;

        var result = _store.RemoveCertificate(serialNumber);

        result.IsSuccess.Should().BeTrue();
        
        // Verify file was deleted
        var newCount = Directory.GetFiles(_tempDirectory, "*.crt").Length;
        newCount.Should().Be(originalCount - 1);
    }

    [Test]
    public void GetStorePath_ShouldReturnConfiguredPath()
    {
        var path = _store.GetStorePath();

        path.Should().Be(_tempDirectory);
    }

    [Test]
    public void RoundTrip_AddAndRetrieveCertificate_ShouldMaintainCertificateData()
    {
        // Get a test certificate
        var originalCert = TestCertificates.ContentSigner;

        // Add certificate
        var addResult = _store.AddCertificate(originalCert);
        addResult.IsSuccess.Should().BeTrue();

        // Retrieve certificates
        var retrieveResult = _store.GetTrustedCertificates();
        retrieveResult.IsSuccess.Should().BeTrue();
        
        var retrievedCerts = retrieveResult.Value.EnumerateMatches(null).ToList();
        var retrievedCert = retrievedCerts.First(c => c.SerialNumber.Equals(originalCert.SerialNumber));

        // Verify certificate data is preserved
        retrievedCert.SubjectDN.ToString().Should().Be(originalCert.SubjectDN.ToString());
        retrievedCert.IssuerDN.ToString().Should().Be(originalCert.IssuerDN.ToString());
        retrievedCert.SerialNumber.Should().Be(originalCert.SerialNumber);
        retrievedCert.NotBefore.Should().Be(originalCert.NotBefore);
        retrievedCert.NotAfter.Should().Be(originalCert.NotAfter);
    }

    [Test]
    public void GetCertificatesByIssuer_ShouldFilterCorrectly()
    {
        _store.InitializeWithDefaults();

        var result = _store.GetCertificatesByIssuer("Test Root CA");

        result.IsSuccess.Should().BeTrue();
        var certificates = result.Value.EnumerateMatches(null).ToList();
        certificates.Should().HaveCountGreaterThan(0);
        certificates.Should().OnlyContain(cert => cert.IssuerDN.ToString().Contains("Test Root CA"));
    }

    [Test]
    public void IsInitialized_WithEmptyDirectory_ShouldReturnFalse()
    {
        _store.IsInitialized().Should().BeFalse();
    }

    [Test]
    public void IsInitialized_WithCertificateFiles_ShouldReturnTrue()
    {
        _store.InitializeWithDefaults();

        _store.IsInitialized().Should().BeTrue();
    }

    [Test]
    public void Configuration_WithCreateDirectoryDisabled_ShouldNotCreateDirectory()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = new TrustedCertificateStoreOptions
        {
            StorePath = nonExistentPath,
            AutoInitializeWithDefaults = false,
            CreateDirectoryIfNotExists = false
        };

        var store = new TrustedCertificateStore(_logger, Options.Create(options), _certificateProvider);

        // Get a test certificate
        var testCert = TestCertificates.RootCA;
        var result = store.AddCertificate(testCert);

        result.IsFailure.Should().BeTrue(); // Should fail because directory doesn't exist
        Directory.Exists(nonExistentPath).Should().BeFalse();
    }

    /// <summary>
    /// Simple test certificate provider that returns the static test certificates.
    /// </summary>
    private class SimpleTestCertificateProvider : ITrustedCertificateProvider
    {
        public Result<IStore<Org.BouncyCastle.X509.X509Certificate>> GetDefaultCertificates()
        {
            var certificates = new[]
            {
                TestCertificates.RootCA,
                TestCertificates.IntermediateCA,
                TestCertificates.ContentSigner
            };
            
            return Result.Success(CollectionUtilities.CreateStore(certificates));
        }

        public Result<IStore<Org.BouncyCastle.X509.X509Certificate>> GetCertificatesByName(string namePattern)
        {
            var allCerts = new[]
            {
                TestCertificates.RootCA,
                TestCertificates.IntermediateCA,
                TestCertificates.ContentSigner
            };
            
            var filtered = allCerts.Where(cert =>
                cert.SubjectDN.ToString().Contains(namePattern, StringComparison.OrdinalIgnoreCase) ||
                cert.IssuerDN.ToString().Contains(namePattern, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Result.Success(CollectionUtilities.CreateStore(filtered));
        }
    }
}