// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.NFIQ2;

[TestFixture]
internal sealed class CaptureImageScoringFailureTests
{
    [Test]
    public void UnassessableProcessingRetainsRealClassStatusAndExplicitNativeDiagnostics()
    {
        using var services = new ScoringTestServices();
        var pixels = ProcessingFrameFixtures
            .PublicFinger()
            .Select(value => (byte)(192 + value * 63 / 255))
            .ToArray();
        var frame = ProcessingFrameFixtures.NormalizedFrame(pixels);
        var prepared = services
            .Processor.PrepareFrame(
                frame,
                ProcessingContractFixtures.Request(brightness: 100),
                CancellationToken.None
            )
            .Value;
        prepared.Artifact.Pixels.Should().OnlyContain(value => value == 255);
        var scored = services.Processor.Assess(prepared, CancellationToken.None).Value;
        scored.Processed.Evidence.IsAssessed.Should().BeFalse();
        scored.Processed.Evidence.Nfiq1.Status.Should().NotBe(FingerprintQualityStatus.Assessed);
        scored.Processed.Evidence.Nfiq2.HasNoValue.Should().BeTrue();
        scored
            .Processed.Evidence.Diagnostics.Should()
            .Contain(error => error.Code == FingerprintErrorCodes.QualityImageNotAssessable);
        services.Counts.Should().Be((1, 1, 0));
    }

    [Test]
    public void OperationalFailureAbortsInsteadOfBecomingAnUnavailableImage()
    {
        using var services = new ScoringTestServices();
        using var engine = new Nfiq2Engine();
        engine.Dispose();
        var processor = new CaptureProcessor(
            new(services.Engine, services.Nfiq1),
            new Nfiq2FingerprintQualityProvider(engine)
        );
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        var result = processor.EvaluateFrame(
            frame,
            ProcessingContractFixtures.Request(),
            CancellationToken.None
        );
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(FingerprintErrorCodes.QualityEngineFailed);
        services.Engine.Extractions.Should().Be(0);
    }
}
