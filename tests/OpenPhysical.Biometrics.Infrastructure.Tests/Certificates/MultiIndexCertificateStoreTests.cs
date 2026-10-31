namespace OpenPhysical.Biometrics.Infrastructure.Tests.Certificates;

using System.Linq;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using OpenPhysical.Biometrics.Core.Certificates;
using OpenPhysical.Biometrics.Infrastructure.Certificates;
using Org.BouncyCastle.Math;

/// <summary>
/// Tests for the MultiIndexCertificateStore implementation.
/// </summary>
[TestFixture]
internal class MultiIndexCertificateStoreTests
{
    private ILogger<MultiIndexCertificateStore> _logger = null!;
    private TrustedCertificateProvider _provider = null!;
    private MultiIndexCertificateStore _store = null!;

    [SetUp]
    public void Setup()
    {
        _logger = Substitute.For<ILogger<MultiIndexCertificateStore>>();
        _provider = new TrustedCertificateProvider(
            Substitute.For<ILogger<TrustedCertificateProvider>>()
        );
        _store = new MultiIndexCertificateStore(
            _logger,
            new Lazy<ITrustedCertificateProvider>(() => _provider)
        );
    }

    [Test]
    public void MultiIndexCertificateStore_WhenInitializedWithDefaults_ShouldStoreEveryDistinctCertificate()
    {
        // Act
        var result = _store.CreateWithDefaults();

        // Assert
        result.Should().Succeed();
        var store = result.Value;
        Assert.That(
            store.CertificateCount,
            Is.EqualTo(ExpectedDistinctCertificates().Count),
            "every distinct certificate must be retained by its encoded fingerprint"
        );

        // Verify we can retrieve all certificates
        var allCertsResult = store.GetAllCertificates();
        allCertsResult.Should().Succeed();
        Assert.That(allCertsResult.Value.Length, Is.EqualTo(ExpectedDistinctCertificates().Count));

        // Log certificate details
        foreach (var cert in allCertsResult.Value)
        {
            TestContext.WriteLine(
                $"Stored certificate: {cert.SubjectDN} (Serial: {cert.SerialNumber})"
            );
        }
    }

    [Test]
    public void MultiIndexCertificateStore_ShouldHandleDuplicateSerialNumbers()
    {
        // Arrange
        var storeResult = _store.CreateWithDefaults();
        Assert.That(storeResult.IsSuccess, Is.True, "CreateWithDefaults should succeed");
        var store = storeResult.Value;

        // Distinct issuers and subjects may legitimately reuse the same serial number.
        var first = CreateCollisionCertificate("CN=First serial collision");
        var second = CreateCollisionCertificate("CN=Second serial collision");
        store = store.CreateWithCertificate(first).Value.CreateWithCertificate(second).Value;
        var result = store.FindBySerialNumber(first.SerialNumber);
        result.Should().Succeed();
        Assert.That(
            result.Value.Select(c => Convert.ToHexString(c.GetEncoded())),
            Is.EquivalentTo(
                new[] { first, second }.Select(c => Convert.ToHexString(c.GetEncoded()))
            )
        );
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
        TestContext.WriteLine(
            $"Found {result.Value.Length} certificate(s) by SKI: {Convert.ToHexString(skiBytes)}"
        );
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
        var result = store.FindBySerialNumberAndIssuer(
            testCert.SerialNumber,
            testCert.IssuerDN.ToString()
        );

        // Assert
        Assert.That(result.IsSuccess, Is.True, "FindBySerialNumberAndIssuer should succeed");
        Assert.That(result.Value.HasValue, Is.True);
        Assert.That(
            result.Value.Value.SubjectDN.ToString(),
            Is.EqualTo(testCert.SubjectDN.ToString())
        );
    }

    [Test]
    public void MultiIndexCertificateStore_ShouldFindBySubjectDN()
    {
        // Arrange
        var storeResult = _store.CreateWithDefaults();
        Assert.That(storeResult.IsSuccess, Is.True, "CreateWithDefaults should succeed");
        var store = storeResult.Value;

        // Act
        var result = store.FindBySubjectDN(
            "C=US,O=U.S. Government,OU=ICAM Test Cards,CN=ICAM Test Card Root CA"
        );

        // Assert
        Assert.That(result.IsSuccess, Is.True, "FindBySubjectDN should succeed");
        Assert.That(
            result.Value.Length,
            Is.GreaterThanOrEqualTo(1),
            "should find at least one root CA"
        );
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
        var issuerCertsResult = store.GetCertificatesByIssuer(
            ExpectedDistinctCertificates().First().IssuerDN.ToString()
        );

        // Assert
        Assert.That(trustedCertsResult.IsSuccess, Is.True, "GetTrustedCertificates should succeed");
        var certCount = 0;
        foreach (var cert in trustedCertsResult.Value.EnumerateMatches(null))
        {
            certCount++;
        }
        Assert.That(certCount, Is.EqualTo(ExpectedDistinctCertificates().Count));

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
        Assert.That(
            removeResult.IsFailure,
            Is.True,
            "RemoveCertificate should fail on immutable store"
        );
        Assert.That(removeResult.Error, Does.Contain("immutable"));
    }

    private IReadOnlyList<Org.BouncyCastle.X509.X509Certificate> ExpectedDistinctCertificates() =>
        _provider
            .GetDefaultCertificates()
            .Value.EnumerateMatches(null)
            .DistinctBy(c => Convert.ToHexString(c.GetEncoded()))
            .ToArray();

    private static Org.BouncyCastle.X509.X509Certificate CreateCollisionCertificate(string subject)
    {
        var generator = new Org.BouncyCastle.X509.X509V3CertificateGenerator();
        generator.SetSerialNumber(new BigInteger("424242"));
        generator.SetIssuerDN(TestCertificates.RootCA.SubjectDN);
        generator.SetSubjectDN(new Org.BouncyCastle.Asn1.X509.X509Name(subject));
        generator.SetNotBefore(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        generator.SetNotAfter(new DateTime(2034, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        generator.SetPublicKey(TestCertificates.ContentSignerKeyPair.Public);
        return generator.Generate(
            new Org.BouncyCastle.Crypto.Operators.Asn1SignatureFactory(
                "SHA256WITHRSA",
                TestCertificates.RootKeyPair.Private
            )
        );
    }
}
