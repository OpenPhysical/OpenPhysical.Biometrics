namespace OpenPhysical.Biometrics.Infrastructure.Tests.Certificates;

using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Infrastructure.Certificates;
using Org.BouncyCastle.Math;

[TestFixture]
public class TestCertificatesStaticClassTests
{
    [Test]
    public void TestCertificates_RootCA_ShouldHaveExpectedProperties()
    {
        var cert = TestCertificates.RootCA;

        cert.Should().NotBeNull();
        cert.SubjectDN.ToString().Should().Contain("Test Root CA");
        cert.IssuerDN.ToString().Should().Be(cert.SubjectDN.ToString()); // Self-signed
        cert.SerialNumber.Should().Be(BigInteger.One);
        cert.NotBefore.Should().Be(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        cert.NotAfter.Should().Be(new DateTime(2034, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void TestCertificates_IntermediateCA_ShouldHaveExpectedProperties()
    {
        var cert = TestCertificates.IntermediateCA;

        cert.Should().NotBeNull();
        cert.SubjectDN.ToString().Should().Contain("Test Intermediate CA");
        cert.IssuerDN.ToString().Should().Contain("Test Root CA");
        cert.SerialNumber.Should().Be(BigInteger.Two);
        cert.NotBefore.Should().Be(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        cert.NotAfter.Should().Be(new DateTime(2029, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void TestCertificates_ContentSigner_ShouldHaveExpectedProperties()
    {
        var cert = TestCertificates.ContentSigner;

        cert.Should().NotBeNull();
        cert.SubjectDN.ToString().Should().Contain("Test Content Signer");
        cert.IssuerDN.ToString().Should().Contain("Test Intermediate CA");
        cert.SerialNumber.Should().Be(BigInteger.Three);
        cert.NotBefore.Should().Be(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        cert.NotAfter.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void TestCertificates_AllCertificates_ShouldBeConsistent()
    {
        // Multiple accesses should return the same certificate instances
        var rootCA1 = TestCertificates.RootCA;
        var rootCA2 = TestCertificates.RootCA;
        
        var intermediateCA1 = TestCertificates.IntermediateCA;
        var intermediateCA2 = TestCertificates.IntermediateCA;
        
        var contentSigner1 = TestCertificates.ContentSigner;
        var contentSigner2 = TestCertificates.ContentSigner;

        rootCA1.Should().BeSameAs(rootCA2);
        intermediateCA1.Should().BeSameAs(intermediateCA2);
        contentSigner1.Should().BeSameAs(contentSigner2);
    }

    [Test]
    public void TestCertificates_KeyPairs_ShouldBeConsistent()
    {
        // Multiple accesses should return the same key pair instances
        var rootKeyPair1 = TestCertificates.RootKeyPair;
        var rootKeyPair2 = TestCertificates.RootKeyPair;
        
        var intermediateKeyPair1 = TestCertificates.IntermediateKeyPair;
        var intermediateKeyPair2 = TestCertificates.IntermediateKeyPair;
        
        var contentSignerKeyPair1 = TestCertificates.ContentSignerKeyPair;
        var contentSignerKeyPair2 = TestCertificates.ContentSignerKeyPair;

        rootKeyPair1.Should().BeSameAs(rootKeyPair2);
        intermediateKeyPair1.Should().BeSameAs(intermediateKeyPair2);
        contentSignerKeyPair1.Should().BeSameAs(contentSignerKeyPair2);
    }

    [Test]
    public void TestCertificates_CertificateChain_ShouldBeValid()
    {
        var rootCA = TestCertificates.RootCA;
        var intermediateCA = TestCertificates.IntermediateCA;
        var contentSigner = TestCertificates.ContentSigner;

        // Verify chain relationships
        intermediateCA.IssuerDN.Should().Be(rootCA.SubjectDN);
        contentSigner.IssuerDN.Should().Be(intermediateCA.SubjectDN);
    }

    [Test]
    public void TestCertificates_AllCertificates_ShouldBeCurrentlyValid()
    {
        var now = DateTime.UtcNow;

        TestCertificates.RootCA.IsValid(now).Should().BeTrue();
        TestCertificates.IntermediateCA.IsValid(now).Should().BeTrue();
        TestCertificates.ContentSigner.IsValid(now).Should().BeTrue();
    }
}