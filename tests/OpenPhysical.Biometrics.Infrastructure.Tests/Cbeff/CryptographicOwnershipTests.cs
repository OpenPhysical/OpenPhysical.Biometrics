using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Infrastructure.Cbeff;
using OpenPhysical.Biometrics.Infrastructure.Extensions;

namespace OpenPhysical.Biometrics.Infrastructure.Tests.Cbeff;

/// <summary>Checks the boundary between biometric processing and application cryptography.</summary>
[TestFixture]
internal sealed class CryptographicOwnershipTests
{
    /// <summary>Checks that unsigned construction resolves without application signing services.</summary>
    [Test]
    public void UnsignedBuilderDoesNotRequireAnApplicationSigner()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCbeffServices();
        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetRequiredService<ICbeffBuilder>(), Is.Not.Null);
        Assert.That(provider.GetService<ICbeffSigner>(), Is.Null);
        Assert.That(provider.GetService<ICbeffVerifier>(), Is.Null);
    }

    /// <summary>Checks that processing assemblies leave signing and trust decisions to the host.</summary>
    [Test]
    public void ProcessingAssemblyDoesNotContainApplicationCryptography()
    {
        Assert.That(
            typeof(ICbeffBuilder).Assembly.GetType(
                "OpenPhysical.Biometrics.Core.Certificates.TrustedCertificateStore"
            ),
            Is.Null,
            "File-backed trust stores belong to the application."
        );
        var assembly = typeof(CbeffSerializer).Assembly;
        Assert.Multiple(() =>
        {
            foreach (
                var name in new[]
                {
                    "OpenPhysical.Biometrics.Infrastructure.Cbeff.CbeffSignerService",
                    "OpenPhysical.Biometrics.Infrastructure.Cbeff.CbeffVerifierService",
                    "OpenPhysical.Biometrics.Infrastructure.Cbeff.CertificateDiscoveryService",
                    "OpenPhysical.Biometrics.Infrastructure.Cbeff.PivCrossValidationService",
                    "OpenPhysical.Biometrics.Infrastructure.Certificates.MultiIndexCertificateStore",
                }
            )
                Assert.That(assembly.GetType(name), Is.Null, name);
        });
    }
}
