// SPDX-License-Identifier: GPL-2.0-or-later
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Middleware.Infrastructure;
using OpenPhysical.Middleware.Infrastructure.Trust;
using OpenPhysical.Workstation.Application.Verification;
using OpenPhysical.Workstation.Infrastructure;
using OpenPhysical.Workstation.Infrastructure.Verification;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class CaptureCompositionTests
{
    [Test]
    public void NativeVerificationDoesNotRegisterCaptureOrGeneralCardMutationServices()
    {
        var services = new ServiceCollection().AddWorkstationCardVerification();
        Assert.That(
            services.Any(value => value.ServiceType == typeof(CaptureDriverRegistry)),
            Is.False
        );
        Assert.That(
            services.Any(value => value.ServiceType == typeof(WorkstationCaptureDevices)),
            Is.False
        );
        Assert.That(
            services.Any(value => value.ServiceType == typeof(IGuidedVerificationRunner)),
            Is.False
        );
        Assert.That(services.Any(value => value.ServiceType == typeof(PivApduExecutor)), Is.False);
        Assert.That(
            typeof(WorkstationCardVerificationRegistration)
                .Assembly.GetReferencedAssemblies()
                .Any(value =>
                    value.Name
                        is "OpenPhysical.Biometrics.Capture.Futronic"
                            or "OpenPhysical.Biometrics.Capture.Usb"
                            or "OpenPhysical.Cards"
                            or "OpenPhysical.Issuance"
                ),
            Is.False
        );
    }

    [Test]
    public void BoundedProofServicesResolveWithoutExposingAGeneralExecutor()
    {
        var services = new ServiceCollection().AddWorkstationCardVerification();
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true }
        );
        provider.GetRequiredService<PinCeremonyService>().Should().NotBeNull();
        provider.GetRequiredService<LiveCredentialVerificationService>().Should().NotBeNull();
        provider.GetServices<PivApduExecutor>().Should().BeEmpty();
    }

    [Test]
    public void StudioExplicitlyRegistersTheExistingCaptureWorkflow()
    {
        var services = new ServiceCollection()
            .AddWorkstationCardVerification()
            .AddWorkstationCapture()
            .Value;
        Assert.That(
            services.Count(value => value.ServiceType == typeof(CaptureDriverRegistry)),
            Is.EqualTo(1)
        );
        Assert.That(
            services.Count(value => value.ServiceType == typeof(WorkstationCaptureDevices)),
            Is.EqualTo(1)
        );
        Assert.That(
            services.Count(value => value.ServiceType == typeof(IGuidedVerificationRunner)),
            Is.EqualTo(1)
        );
        Assert.That(
            typeof(WorkstationCaptureDevices).Assembly.GetName().Name,
            Is.EqualTo("OpenPhysical.Biometrics.Desktop")
        );
        Assert.That(services.Any(value => value.ServiceType == typeof(PivApduExecutor)), Is.False);
    }
}
