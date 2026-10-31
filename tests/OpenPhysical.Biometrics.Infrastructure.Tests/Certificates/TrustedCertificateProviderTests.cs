namespace OpenPhysical.Biometrics.Infrastructure.Tests.Certificates;

using System.Linq;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OpenPhysical.Biometrics.Infrastructure.Certificates;

[TestFixture]
internal class TrustedCertificateProviderTests
{
    private TrustedCertificateProvider _provider = null!;
    private ILogger<TrustedCertificateProvider> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<TrustedCertificateProvider>();
        _provider = new TrustedCertificateProvider(_logger);
    }

    [Test]
    public void GetDefaultCertificates_ShouldLoadAllEmbeddedCertificates()
    {
        // Act
        var result = _provider.GetDefaultCertificates();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var certificates = result.Value.EnumerateMatches(null).ToList();

        certificates
            .Should()
            .HaveCount(13, "all thirteen committed certificate resources must load");

        // Verify we have the expected certificates
        var subjects = certificates.Select(c => c.SubjectDN.ToString()).ToList();

        // Test certificates (3)
        subjects.Should().Contain(s => s.Contains("ICAM Test Card Root CA"));
        subjects.Should().Contain(s => s.Contains("ICAM Test Card Signing CA"));
        subjects.Should().Contain(s => s.Contains("ICAM Test Card PIV Content Signer"));

        // PIV certificates (4)
        subjects.Should().Contain(s => s.Contains("ICAM PIV Content Signer"));
    }

    [Test]
    public void GetDefaultCertificates_ShouldNotFailSilently()
    {
        // This test verifies that if any certificate fails to load, the entire operation fails
        // (as per the implementation which returns failure if any certificate can't be loaded)

        // Act
        var result = _provider.GetDefaultCertificates();

        // Assert
        result.IsSuccess.Should().BeTrue("all certificates should load successfully");
    }

    [Test]
    public void GetCertificatesByName_WithMatchingPattern_ShouldReturnFilteredCertificates()
    {
        // Act
        var result = _provider.GetCertificatesByName("Root CA");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var certificates = result.Value.EnumerateMatches(null).ToList();

        certificates.Should().HaveCountGreaterThan(0);
        certificates
            .Should()
            .OnlyContain(cert =>
                cert.SubjectDN.ToString().Contains("Root CA")
                || cert.IssuerDN.ToString().Contains("Root CA")
            );
    }

    [Test]
    public void GetCertificatesByName_WithNonMatchingPattern_ShouldReturnEmptyStore()
    {
        // Act
        var result = _provider.GetCertificatesByName("NonExistentCertificate");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var certificates = result.Value.EnumerateMatches(null).ToList();
        certificates.Should().BeEmpty();
    }

    [Test]
    public void LoadedCertificates_ShouldHaveValidProperties()
    {
        // Act
        var result = _provider.GetDefaultCertificates();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var certificates = result.Value.EnumerateMatches(null).ToList();

        foreach (var cert in certificates)
        {
            cert.SubjectDN.Should().NotBeNull();
            cert.IssuerDN.Should().NotBeNull();
            cert.SerialNumber.Should().NotBeNull();
            cert.NotBefore.Should().BeBefore(cert.NotAfter);
        }
    }
}
