// SPDX-License-Identifier: GPL-2.0-or-later
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.NFIQ2;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class SupplementalQualityTests
{
    [Test]
    public void DesktopAssessesTheSupplementalScoreForFreshCaptures()
    {
        using var host = DesktopServices.CreateHost();
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        var capture = ProcessingContractFixtures.Capture(
            host.Services.GetRequiredService<ICaptureProcessor>(),
            frame,
            FingerPosition.RightIndex
        );
        capture.Should().Succeed();
        capture.Value.Nfiq2.HasValue.Should().BeTrue();
        capture.Value.Nfiq2.Value.Score.Should().BeInRange(0, 100);
        capture.Value.Frame.Should().BeSameAs(frame);
        capture.Value.ExtractorVersion.Value.Should().Be("NBIS 5.0.0 MINDTCT V2");
    }

    [Test]
    public void CachedCaptureReusesOneSupplementalAssessmentOfItsExactPixels()
    {
        using var services = new ServiceCollection()
            .AddFingerprintProcessing()
            .AddNfiq1FingerprintQuality()
            .AddNfiq2FingerprintQuality()
            .BuildServiceProvider();
        var quality = new CountingUnifiedQualityProvider(
            services.GetRequiredService<IFingerprintUnifiedQualityProvider>()
        );
        var processor = new CaptureProcessor(
            services.GetRequiredService<FingerprintPreparationService>(),
            quality
        );
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        var result = ProcessingContractFixtures.Capture(
            processor,
            frame,
            FingerPosition.RightIndex
        );
        result.Should().Succeed();
        var capture = result.Value;
        quality.Image.Value.Pixels.ToArray().Should().Equal(frame.Pixels);
        quality.Image.Value.DotsPerInch.Should().Be(500);
        UnsignedTemplateService.Create([capture], new(0x0103, 0, 0)).Should().Succeed();
        quality.Calls.Should().Be(1);
        capture.Nfiq2.HasValue.Should().BeTrue();
    }

    [Test]
    public void AFailedSupplementalEngineCannotProduceAnApparentlyCompleteFreshCapture()
    {
        using var services = new ServiceCollection()
            .AddFingerprintProcessing()
            .AddNfiq1FingerprintQuality()
            .BuildServiceProvider();
        using var engine = new Nfiq2Engine();
        engine.Dispose();
        var processor = new CaptureProcessor(
            services.GetRequiredService<FingerprintPreparationService>(),
            new Nfiq2FingerprintQualityProvider(engine)
        );
        ProcessingContractFixtures
            .Capture(
                processor,
                ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger()),
                FingerPosition.RightIndex
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(FingerprintErrorCodes.QualityEngineFailed);
    }
}
