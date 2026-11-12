// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class CaptureExactQualityFailureTests
{
    [Test]
    public void SingleImageReturnsActualNativeUnassessabilityWithoutASelectionWrapper()
    {
        using var services = new ScoringTestServices();
        var pixels = ProcessingFrameFixtures
            .PublicFinger()
            .Select(value => (byte)(192 + value * 63 / 255))
            .ToArray();
        var frame = ProcessingFrameFixtures.NormalizedFrame(pixels);
        var result = services.Processor.EvaluateFrame(
            frame,
            ProcessingContractFixtures.Request(brightness: 100),
            CancellationToken.None
        );
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(FingerprintErrorCodes.QualityImageNotAssessable);
        services.Counts.Should().Be((1, 1, 0));
    }
}
