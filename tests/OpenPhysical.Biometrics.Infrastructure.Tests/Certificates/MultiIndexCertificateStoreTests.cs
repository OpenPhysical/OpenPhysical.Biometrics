namespace OpenPhysical.Biometrics.Infrastructure.Tests.Certificates;

using System.Linq;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OpenPhysical.Biometrics.Infrastructure.Certificates;
using Org.BouncyCastle.Math;

/// <summary>
/// Tests for the MultiIndexCertificateStore implementation.
/// </summary>
[TestFixture]
public class MultiIndexCertificateStoreTests
{
    private ILogger<MultiIndexCertificateStore> _logger = null!;
    private TrustedCertificateProvider _provider = null!;
    private MultiIndexCertificateStore _store = null!;

    [SetUp]
    public void Setup()
    {
        _logger = NSubstitute.Substitute.For<ILogger<MultiIndexCertificateStore>>();
        _provider = new TrustedCertificateProvider(NSubstitute.Substitute.For<ILogger<TrustedCertificateProvider>>());
        _store = new MultiIndexCertificateStore(_logger, new Lazy<ITrustedCertificateProvider>(() => _provider));
    }

    [Test]
    public void MultiIndexCertificateStore_WhenInitializedWithDefaults_ShouldStoreAllSevenCertificates()
    {
        // Act
        var result = _store.CreateWithDefaults();

        // Assert
        result.Should().BeSuccess();
        var store = result.Value;
        Assert.That(store.CertificateCount, Is.EqualTo(7), "all 7 ICAM certificates should be stored with unique fingerprints");

        // Verify we can retrieve all certificates
        var allCertsResult = store.GetAllCertificates();
        allCertsResult.Should().BeSuccess();
        Assert.That(allCertsResult.Value.Length, Is.EqualTo(7));

        // Log certificate details
        foreach (var cert in allCertsResult.Value)
        {
            TestContext.WriteLine($"Stored certificate: {cert.SubjectDN} (Serial: {cert.SerialNumber})");
        }
    }

    [Test]
    public void MultiIndexCertificateStore_ShouldHandleDuplicateSerialNumbers()
    {
        // Arrange
        var storeResult = _store.CreateWithDefaults();
        Assert.That(storeResult.IsSuccess, Is.True, "CreateWithDefaults should succeed");
        var store = storeResult.Value;

        // Known duplicate serial numbers from test data
        var duplicateSerials = new[]
        {
            new BigInteger("5696"), // Two root CAs
            new BigInteger("7746183142974032771"), // Two signing CAs
            new BigInteger("600000000000000000ca", 16) // Two content signers
        };

        // Act & Assert
        foreach (var serial in duplicateSerials)
        {
            var result = store.FindBySerialNumber(serial);
            result.Should().BeSuccess();
            Assert.That(result.Value.Length, Is.EqualTo(2), $"serial number {serial} should return 2 certificates");
            
            // Verify they have different subjects
            var subjects = result.Value.Select(c => c.SubjectDN.ToString()).ToList();
            Assert.That(subjects.Count, Is.EqualTo(2));
            Assert.That(subjects.Distinct().Count(), Is.EqualTo(2), "duplicate serial numbers should have different subjects");
        }
    }

    [Test]
    public void MultiIndexCertificateStore_ShouldFindBySubjectKeyIdentifier()
    {
        // Arrange
        var storeResult = _store.CreateWithDefaults();
        Assert.That(storeResult.IsSuccess, Is.True, "CreateWithDefaults should succeed");
        var store = storeResult.Value;

        // Get metadata to find a valid SKI
        var metadataResult = store.GetAllMetadata();
        Assert.That(metadataResult.IsSuccess, Is.True, "GetAllMetadata should succeed");
        
        var certWithSki = metadataResult.Value.FirstOrDefault(m => m.SubjectKeyIdentifier.HasValue);
        Assert.That(certWithSki, Is.Not.Null, "at least one certificate should have SKI");

        var skiBytes = certWithSki!.SubjectKeyIdentifier.Value.ToArray();

        // Act
        var result = store.FindBySubjectKeyIdentifier(skiBytes);

        // Assert
        Assert.That(result.IsSuccess, Is.True, "FindBySubjectKeyIdentifier should succeed");
        Assert.That(result.Value.Length, Is.GreaterThanOrEqualTo(1));
        TestContext.WriteLine($"Found {result.Value.Length} certificate(s) by SKI: {Convert.ToHexString(skiBytes)}");
    }

    [Test]
    public void MultiIndexCertificateStore_ShouldFindBySerialAndIssuer()
    {
        // Arrange
        var storeResult = _store.CreateWithDefaults();
        Assert.That(storeResult.IsSuccess, Is.True, "CreateWithDefaults should succeed");
        var store = storeResult.Value;

        // Use a known certificate
        var allCertsResult = store.GetAllCertificates();
        Assert.That(allCertsResult.IsSuccess, Is.True, "GetAllCertificates should succeed");
        var testCert = allCertsResult.Value.First();

        // Act
        var result = store.FindBySerialNumberAndIssuer(testCert.SerialNumber, testCert.IssuerDN.ToString());

        // Assert
        Assert.That(result.IsSuccess, Is.True, "FindBySerialNumberAndIssuer should succeed");
        Assert.That(result.Value.HasValue, Is.True);
        Assert.That(result.Value.Value.SubjectDN.ToString(), Is.EqualTo(testCert.SubjectDN.ToString()));
    }

    [Test]
    public void MultiIndexCertificateStore_ShouldFindBySubjectDN()
    {
        // Arrange
        var storeResult = _store.CreateWithDefaults();
        Assert.That(storeResult.IsSuccess, Is.True, "CreateWithDefaults should succeed");
        var store = storeResult.Value;

        // Act
        var result = store.FindBySubjectDN("C=US,O=U.S. Government,OU=ICAM Test Cards,CN=ICAM Test Card Root CA");

        // Assert
        Assert.That(result.IsSuccess, Is.True, "FindBySubjectDN should succeed");
        Assert.That(result.Value.Length, Is.GreaterThanOrEqualTo(1), "should find at least one root CA");
    }

    [Test]
    public void MultiIndexCertificateStore_ShouldProvideBackwardCompatibility()
    {
        // Arrange
        var storeResult = _store.CreateWithDefaults();
        Assert.That(storeResult.IsSuccess, Is.True, "CreateWithDefaults should succeed");
        var store = storeResult.Value;

        // Act - Test ITrustedCertificateStore methods
        var trustedCertsResult = store.GetTrustedCertificates();
        var issuerCertsResult = store.GetCertificatesByIssuer("ICAM Test Cards");

        // Assert
        Assert.That(trustedCertsResult.IsSuccess, Is.True, "GetTrustedCertificates should succeed");
        var certCount = 0;
        foreach (var cert in trustedCertsResult.Value.EnumerateMatches(null))
        {
            certCount++;
        }
        Assert.That(certCount, Is.EqualTo(7));

        Assert.That(issuerCertsResult.IsSuccess, Is.True, "GetCertificatesByIssuer should succeed");
        var issuerCount = 0;
        foreach (var cert in issuerCertsResult.Value.EnumerateMatches(null))
        {
            issuerCount++;
        }
        Assert.That(issuerCount, Is.GreaterThan(0));
    }

    [Test]
    public void MultiIndexCertificateStore_ShouldBeImmutable()
    {
        // Arrange
        var storeResult = _store.CreateWithDefaults();
        Assert.That(storeResult.IsSuccess, Is.True, "CreateWithDefaults should succeed");
        var store = storeResult.Value;

        // Act
        var addResult = store.AddCertificate(null!);
        var removeResult = store.RemoveCertificate("test");

        // Assert
        Assert.That(addResult.IsFailure, Is.True, "AddCertificate should fail on immutable store");
        Assert.That(addResult.Error, Does.Contain("immutable"));
        Assert.That(removeResult.IsFailure, Is.True, "RemoveCertificate should fail on immutable store");
        Assert.That(removeResult.Error, Does.Contain("immutable"));
    }
}