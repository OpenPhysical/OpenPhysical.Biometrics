namespace OpenPhysical.Biometrics.Infrastructure.Tests.Certificates;

using System.Linq;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OpenPhysical.Biometrics.Core.Certificates;
using OpenPhysical.Biometrics.Infrastructure.Certificates;

[TestFixture]
public class InMemoryTrustedCertificateStoreTests
{
    private InMemoryTrustedCertificateStore _store;
    private ILogger<InMemoryTrustedCertificateStore> _storeLogger;
    private ILogger<TrustedCertificateProvider> _providerLogger;
    private ITrustedCertificateProvider _certificateProvider;

    [SetUp]
    public void SetUp()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _storeLogger = loggerFactory.CreateLogger<InMemoryTrustedCertificateStore>();
        _providerLogger = loggerFactory.CreateLogger<TrustedCertificateProvider>();
        
        _certificateProvider = new TrustedCertificateProvider(_providerLogger);
        _store = new InMemoryTrustedCertificateStore(
            _storeLogger, 
            new Lazy<ITrustedCertificateProvider>(() => _certificateProvider),
            autoInitializeWithDefaults: false);
    }

    [Test]
    public void TrustedCertificateProvider_ShouldLoadExactlySevenCertificates()
    {
        // This test verifies that the TrustedCertificateProvider loads all 7 embedded certificates
        
        // Act
        var result = _certificateProvider.GetDefaultCertificates();

        // Assert
        result.IsSuccess.Should().BeTrue("provider should successfully load all certificates");
        
        var certificates = result.Value.EnumerateMatches(null).ToList();
        certificates.Should().HaveCount(7, "there are exactly 7 embedded certificate resources");
        
        // Verify each certificate has expected properties
        certificates.Should().OnlyContain(cert => cert != null);
        certificates.Should().OnlyContain(cert => cert.SubjectDN != null);
        certificates.Should().OnlyContain(cert => cert.SerialNumber != null);
        
        // Log the loaded certificates for verification
        foreach (var cert in certificates)
        {
            TestContext.WriteLine($"Loaded certificate: {cert.SubjectDN} (Serial: {cert.SerialNumber})");
        }
    }

    [Test]
    public void InMemoryTrustedCertificateStore_WhenInitializedWithDefaults_ShouldStoreUniqueSerialNumbers()
    {
        // This test verifies that InMemoryTrustedCertificateStore properly stores certificates 
        // when initialized with defaults. Note: The store uses serial numbers as keys,
        // so duplicate serial numbers will overwrite previous certificates.
        
        // Act
        var createResult = _store.CreateWithDefaults();

        // Assert
        createResult.IsSuccess.Should().BeTrue("store should be created successfully with defaults");
        
        var initializedStore = createResult.Value;
        initializedStore.Should().NotBeNull();
        initializedStore.IsInitialized().Should().BeTrue("store should be initialized after loading defaults");
        
        // Get the expected unique count
        var providerCertsResult = _certificateProvider.GetDefaultCertificates();
        var providerCerts = providerCertsResult.Value.EnumerateMatches(null).ToList();
        var uniqueSerialCount = providerCerts.Select(c => c.SerialNumber.ToString()).Distinct().Count();
        
        initializedStore.Count.Should().Be(uniqueSerialCount, 
            "store should contain certificates with unique serial numbers (duplicates are overwritten)");
        
        // Verify we can retrieve all certificates
        var getCertsResult = initializedStore.GetTrustedCertificates();
        getCertsResult.IsSuccess.Should().BeTrue("should successfully retrieve certificates from store");
        
        var storedCertificates = getCertsResult.Value.EnumerateMatches(null).ToList();
        storedCertificates.Should().HaveCount(uniqueSerialCount, 
            "retrieved certificates should match the count of unique serial numbers");
    }

    [Test]
    public void InMemoryTrustedCertificateStore_ShouldBeImmutable()
    {
        // This test verifies the immutable behavior of the store
        
        // Arrange
        var createResult = _store.CreateWithDefaults();
        var initializedStore = createResult.Value;
        var initialCount = initializedStore.Count;
        
        // Act - Try to add a certificate through the mutable interface
        var providerCerts = _certificateProvider.GetDefaultCertificates();
        var dummyCert = providerCerts.Value.EnumerateMatches(null).First();
        var addResult = initializedStore.AddCertificate(dummyCert);
        
        // Assert
        addResult.IsFailure.Should().BeTrue("AddCertificate should fail on immutable store");
        addResult.Error.Should().Contain("immutable");
        initializedStore.Count.Should().Be(initialCount, "store count should not change after failed add");
    }

    [Test]
    public void InMemoryTrustedCertificateStore_EmptyStore_ShouldNotBeInitialized()
    {
        // Test that an empty store is not considered initialized
        
        // Act
        var emptyStoreResult = _store.CreateEmpty();
        
        // Assert
        emptyStoreResult.IsSuccess.Should().BeTrue();
        var emptyStore = emptyStoreResult.Value;
        emptyStore.IsInitialized().Should().BeFalse("empty store should not be initialized");
        emptyStore.Count.Should().Be(0, "empty store should have no certificates");
    }

    [Test]
    public void InMemoryTrustedCertificateStore_GetCertificatesByIssuer_ShouldFilterCorrectly()
    {
        // Test filtering by issuer
        
        // Arrange
        var createResult = _store.CreateWithDefaults();
        var initializedStore = createResult.Value;
        
        // Act
        var rootCaResult = initializedStore.GetCertificatesByIssuer("Root CA");
        
        // Assert
        rootCaResult.IsSuccess.Should().BeTrue();
        var rootCaCerts = rootCaResult.Value.EnumerateMatches(null).ToList();
        rootCaCerts.Should().NotBeEmpty("there should be certificates issued by Root CA");
        rootCaCerts.Should().OnlyContain(cert => cert.IssuerDN.ToString().Contains("Root CA"));
    }

    [Test]
    public void InMemoryTrustedCertificateStore_ShouldHandleDuplicateSerialNumbers()
    {
        // This test investigates the duplicate serial number issue
        
        // Act
        var providerResult = _certificateProvider.GetDefaultCertificates();
        var certificates = providerResult.Value.EnumerateMatches(null).ToList();
        
        // Group certificates by serial number to find duplicates
        var groupedBySerial = certificates.GroupBy(c => c.SerialNumber.ToString())
            .Where(g => g.Count() > 1)
            .ToList();
        
        // Log the findings
        TestContext.WriteLine($"Total certificates from provider: {certificates.Count}");
        TestContext.WriteLine($"Unique serial numbers: {certificates.Select(c => c.SerialNumber.ToString()).Distinct().Count()}");
        TestContext.WriteLine($"Number of groups with duplicate serials: {groupedBySerial.Count}");
        
        foreach (var group in groupedBySerial)
        {
            TestContext.WriteLine($"\nDuplicate serial number: {group.Key}");
            foreach (var cert in group)
            {
                TestContext.WriteLine($"  - Subject: {cert.SubjectDN}");
                TestContext.WriteLine($"    Issuer: {cert.IssuerDN}");
            }
        }
        
        // The InMemoryTrustedCertificateStore uses serial number as the key
        // This explains why only 4 certificates are stored when there are 7 loaded
        var storeResult = _store.CreateWithDefaults();
        var store = storeResult.Value;
        
        // Verify that the store behavior matches expectations given the duplicate serial numbers
        store.Count.Should().Be(certificates.Select(c => c.SerialNumber.ToString()).Distinct().Count(),
            "store count should equal the number of unique serial numbers");
    }
}