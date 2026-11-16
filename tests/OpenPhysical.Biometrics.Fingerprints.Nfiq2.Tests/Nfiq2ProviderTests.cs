// SPDX-License-Identifier: GPL-2.0-or-later
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using OpenPhysical.Biometrics.NBIS;
using OpenPhysical.Biometrics.NFIQ2;

namespace OpenPhysical.Biometrics.Fingerprints.Nfiq2.Tests;

[TestFixture]
[NonParallelizable]
internal sealed class Nfiq2ProviderTests
{
    [Test]
    public void ReusedNativeEnginePreservesPublishedNistScoreAndPixels()
    {
        using var engine = new Nfiq2Engine();
        var provider = new Nfiq2FingerprintQualityProvider(engine);
        var pixels = File.ReadAllBytes(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures/SFinGe_Test01.gray8")
        );
        var image = new FingerprintImage(pixels, 416, 560, 500);
        var first = provider.Assess(image);
        var second = provider.Assess(image);
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Score.Should().Be(54);
        second.Value.Score.Should().Be(first.Value.Score);
        first.Value.AlgorithmVersion.Should().Be("NFIQ 2.3.0; embedded optical/ink model");
        image.Pixels.ToArray().Should().Equal(pixels);
    }

    [Test]
    public void RegistrationReusesOneProviderAndEngine()
    {
        var services = new ServiceCollection();
        services.AddNfiq2FingerprintQuality();
        services.AddNfiq2FingerprintQuality();
        services
            .Count(registration => registration.ServiceType == typeof(INfiq2Engine))
            .Should()
            .Be(1);
        using var container = services.BuildServiceProvider();
        container
            .GetRequiredService<IFingerprintUnifiedQualityProvider>()
            .Should()
            .BeSameAs(container.GetRequiredService<IFingerprintUnifiedQualityProvider>());
        container
            .GetRequiredService<INfiq2Engine>()
            .Should()
            .BeSameAs(container.GetRequiredService<INfiq2Engine>());
        services
            .Should()
            .NotContain(registration =>
                registration.ServiceType == typeof(IFingerprintQualityProvider)
            );
    }

    [Test]
    public void FailedFreshAssessmentReturnsAnErrorInsteadOfAMissingScore()
    {
        using var engine = new Nfiq2Engine();
        var provider = new Nfiq2FingerprintQualityProvider(engine);
        var image = new FingerprintImage(
            Enumerable.Repeat((byte)255, 800 * 750).ToArray(),
            800,
            750,
            500
        );
        var result = provider.Assess(image);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(FingerprintErrorCodes.QualityImageNotAssessable);
    }

    [Test]
    public void DisposedEngineFailureIsAnOperationalError()
    {
        var engine = new Nfiq2Engine();
        var provider = new Nfiq2FingerprintQualityProvider(engine);
        engine.Dispose();
        var image = new FingerprintImage(new byte[64 * 64], 64, 64, 500);
        var result = provider.Assess(image);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(FingerprintErrorCodes.QualityEngineFailed);
    }
}
