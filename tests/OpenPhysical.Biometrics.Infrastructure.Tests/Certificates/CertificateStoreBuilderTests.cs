namespace OpenPhysical.Biometrics.Infrastructure.Tests.Certificates;

using System;
using System.Linq;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OpenPhysical.Biometrics.Core.Certificates;
using OpenPhysical.Biometrics.Infrastructure.Certificates;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.X509;

[TestFixture]
public class CertificateStoreBuilderTests
{
    private ILogger<InMemoryTrustedCertificateStore> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _logger = new LoggerFactory().CreateLogger<InMemoryTrustedCertificateStore>();
    }

    [Test]
    public void EmptyStore_ShouldInitializeEmpty()
    {
        var store = new CertificateStoreBuilder(_logger).Build();

        var result = store.GetTrustedCertificates();
        
        result.IsSuccess.Should().BeTrue();
        var certificates = result.Value.EnumerateMatches(null).ToList();
        certificates.Should().BeEmpty();
        store.GetStorePath().Should().Be("[In-Memory Store]");
    }

    [Test]
    public void WithTestRootCA_ShouldAddRootCertificate()
    {
        var store = new CertificateStoreBuilder(_logger)
            .WithTestRootCA()
            .Build();

        var result = store.GetTrustedCertificates();
        
        result.IsSuccess.Should().BeTrue();
        var certificates = result.Value.EnumerateMatches(null).ToList();
        certificates.Should().HaveCount(1);
        certificates[0].SerialNumber.Should().Be(BigInteger.One);
        certificates[0].SubjectDN.ToString().Should().Contain("Test Root CA");
    }

    [Test]
    public void WithTestIntermediateCA_ShouldAddIntermediateCertificate()
    {
        var store = new CertificateStoreBuilder(_logger)
            .WithTestIntermediateCA()
            .Build();

        var result = store.GetTrustedCertificates();
        
        result.IsSuccess.Should().BeTrue();
        var certificates = result.Value.EnumerateMatches(null).ToList();
        certificates.Should().HaveCount(1);
        certificates[0].SerialNumber.Should().Be(BigInteger.Two);
        certificates[0].SubjectDN.ToString().Should().Contain("Test Intermediate CA");
    }

    [Test]
    public void WithTestContentSigner_ShouldAddContentSignerCertificate()
    {
        var store = new CertificateStoreBuilder(_logger)
            .WithTestContentSigner()
            .Build();

        var result = store.GetTrustedCertificates();
        
        result.IsSuccess.Should().BeTrue();
        var certificates = result.Value.EnumerateMatches(null).ToList();
        certificates.Should().HaveCount(1);
        certificates[0].SerialNumber.Should().Be(BigInteger.Three);
        certificates[0].SubjectDN.ToString().Should().Contain("Test Content Signer");
    }

    [Test]
    public void WithAllTestCertificates_ShouldAddAllThreeCertificates()
    {
        var store = new CertificateStoreBuilder(_logger)
            .WithAllTestCertificates()
            .Build();

        var result = store.GetTrustedCertificates();
        
        result.IsSuccess.Should().BeTrue();
        var certificates = result.Value.EnumerateMatches(null).ToList();
        certificates.Should().HaveCount(3);
        
        var serials = certificates.Select(c => c.SerialNumber).OrderBy(s => s).ToList();
        serials[0].Should().Be(BigInteger.One);
        serials[1].Should().Be(BigInteger.Two);
        serials[2].Should().Be(BigInteger.Three);
    }

    [Test]
    public void WithCertificate_ShouldAddSpecificCertificate()
    {
        var customCert = TestCertificates.IntermediateCA;
        
        var store = new CertificateStoreBuilder(_logger)
            .WithCertificate(customCert)
            .Build();

        var result = store.GetTrustedCertificates();
        
        result.IsSuccess.Should().BeTrue();
        var certificates = result.Value.EnumerateMatches(null).ToList();
        certificates.Should().HaveCount(1);
        certificates[0].Should().Be(customCert);
    }

    [Test]
    public void GetCertificatesByIssuer_ShouldFilterByIssuer()
    {
        var store = new CertificateStoreBuilder(_logger)
            .WithAllTestCertificates()
            .Build();

        var result = store.GetCertificatesByIssuer("Test Root CA");

        result.IsSuccess.Should().BeTrue();
        var certificates = result.Value.EnumerateMatches(null).ToList();
        // Should return certificates issued by Root CA (including self-signed Root CA)
        certificates.Should().HaveCount(2);
        certificates.Should().OnlyContain(cert => cert.IssuerDN.ToString().Contains("Test Root CA"));
        certificates.Should().Contain(cert => cert.SubjectDN.ToString().Contains("Test Root CA")); // Self-signed
        certificates.Should().Contain(cert => cert.SubjectDN.ToString().Contains("Test Intermediate CA"));
    }

    [Test]
    public void RemoveCertificate_ShouldReturnFailureForImmutableStore()
    {
        var store = new CertificateStoreBuilder(_logger)
            .WithAllTestCertificates()
            .Build();

        // Verify we have 3 certificates
        var initialResult = store.GetTrustedCertificates();
        initialResult.Value.EnumerateMatches(null).Count().Should().Be(3);

        // Attempt to remove the intermediate certificate should fail for immutable store
        var removeResult = store.RemoveCertificate("2");
        removeResult.IsFailure.Should().BeTrue();
        removeResult.Error.Should().Contain("immutable");

        // Verify we still have 3 certificates (no mutation occurred)
        var finalResult = store.GetTrustedCertificates();
        var remaining = finalResult.Value.EnumerateMatches(null).ToList();
        remaining.Should().HaveCount(3);
    }

    [Test]
    public void ChainedBuilder_ShouldAccumulateCertificates()
    {
        var store = new CertificateStoreBuilder(_logger)
            .WithTestRootCA()
            .WithCertificate(TestCertificates.ContentSigner)
            .Build();

        var result = store.GetTrustedCertificates();
        
        result.IsSuccess.Should().BeTrue();
        var certificates = result.Value.EnumerateMatches(null).ToList();
        certificates.Should().HaveCount(2);
        
        var serials = certificates.Select(c => c.SerialNumber).OrderBy(s => s).ToList();
        serials[0].Should().Be(BigInteger.One);   // Root CA
        serials[1].Should().Be(BigInteger.Three); // Content Signer
    }

    [Test]
    public void CreateWithCertificate_ShouldCreateNewImmutableInstance()
    {
        var originalStore = new CertificateStoreBuilder(_logger)
            .WithTestRootCA()
            .Build() as InMemoryTrustedCertificateStore;

        originalStore.Should().NotBeNull();

        // Add a certificate using immutable method
        var addResult = originalStore!.CreateWithCertificate(TestCertificates.IntermediateCA);
        addResult.IsSuccess.Should().BeTrue();
        
        var newStore = addResult.Value;
        
        // Original store should be unchanged
        var originalCerts = originalStore.GetTrustedCertificates().Value.EnumerateMatches(null).ToList();
        originalCerts.Should().HaveCount(1);
        
        // New store should have both certificates
        var newCerts = newStore.GetTrustedCertificates().Value.EnumerateMatches(null).ToList();
        newCerts.Should().HaveCount(2);
    }

    [Test]
    public void CreateWithoutCertificate_ShouldCreateNewImmutableInstance()
    {
        var originalStore = new CertificateStoreBuilder(_logger)
            .WithAllTestCertificates()
            .Build() as InMemoryTrustedCertificateStore;

        originalStore.Should().NotBeNull();

        // Remove a certificate using immutable method
        var removeResult = originalStore!.CreateWithoutCertificate("2"); // Intermediate CA serial number
        removeResult.IsSuccess.Should().BeTrue();
        
        var newStore = removeResult.Value;
        
        // Original store should be unchanged
        var originalCerts = originalStore.GetTrustedCertificates().Value.EnumerateMatches(null).ToList();
        originalCerts.Should().HaveCount(3);
        
        // New store should have one less certificate
        var newCerts = newStore.GetTrustedCertificates().Value.EnumerateMatches(null).ToList();
        newCerts.Should().HaveCount(2);
        newCerts.Should().NotContain(c => c.SerialNumber.Equals(BigInteger.Two));
    }
}