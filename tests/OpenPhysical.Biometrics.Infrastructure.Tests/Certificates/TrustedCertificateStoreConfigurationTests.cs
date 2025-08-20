namespace OpenPhysical.Biometrics.Infrastructure.Tests.Certificates;

using System;
using System.IO;
using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Core.Certificates;

[TestFixture]
public class TrustedCertificateStoreConfigurationTests
{
    [Test]
    public void DefaultOptions_ShouldUseUserDataDirectory()
    {
        var options = new TrustedCertificateStoreOptions();

        // Platform-agnostic test - just verify it uses the user's app data folder
        var expectedBase = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        options.StorePath.Should().StartWith(expectedBase);
        options.StorePath.Should().Contain("OpenPhysical");
        options.StorePath.Should().Contain("Biometrics");
        options.StorePath.Should().Contain("TrustedCertificates");
        options.AutoInitializeWithDefaults.Should().BeTrue();
        options.CreateDirectoryIfNotExists.Should().BeTrue();
    }

    [Test]
    public void StorePath_CanBeSet()
    {
        var options = new TrustedCertificateStoreOptions
        {
            StorePath = "/custom/cert/store"
        };

        options.StorePath.Should().Be("/custom/cert/store");
    }

    [Test]
    public void AutoInitializeWithDefaults_CanBeSet()
    {
        var options = new TrustedCertificateStoreOptions
        {
            AutoInitializeWithDefaults = false
        };

        options.AutoInitializeWithDefaults.Should().BeFalse();
    }

    [Test]
    public void CreateDirectoryIfNotExists_CanBeSet()
    {
        var options = new TrustedCertificateStoreOptions
        {
            CreateDirectoryIfNotExists = false
        };

        options.CreateDirectoryIfNotExists.Should().BeFalse();
    }

    [Test]
    public void AllOptions_CanBeConfigured()
    {
        var options = new TrustedCertificateStoreOptions
        {
            StorePath = "/test/path",
            AutoInitializeWithDefaults = false,
            CreateDirectoryIfNotExists = false
        };

        options.StorePath.Should().Be("/test/path");
        options.AutoInitializeWithDefaults.Should().BeFalse();
        options.CreateDirectoryIfNotExists.Should().BeFalse();
    }
}