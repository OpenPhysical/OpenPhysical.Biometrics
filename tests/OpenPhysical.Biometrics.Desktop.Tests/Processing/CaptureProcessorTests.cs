// SPDX-License-Identifier: GPL-2.0-or-later
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class CaptureProcessorTests : IDisposable
{
    private readonly IHost host = DesktopServices.CreateHost();

    public void Dispose() => host.Dispose();

    [Test]
    public void PublicNistFingerprintKeepsItsPixelsAndOnlyRetainsForegroundFeatures()
    {
        var pixels = ProcessingFrameFixtures.PublicFinger();
        var original = pixels.ToArray();
        var frame = ProcessingFrameFixtures.NormalizedFrame(pixels);
        var prepared = ProcessingContractFixtures.Capture(
            Processor(),
            frame,
            FingerPosition.RightIndex
        );
        prepared.Should().Succeed();
        var capture = prepared.Value;
        capture.Frame.Should().BeSameAs(frame);
        capture.Frame.Pixels.Should().Equal(original);
        capture.Processing.Value.ContactDiagnostics.ComponentCount.Should().Be(1);
        capture.ExtractedCount.Value.Should().BeGreaterThan(10);
        capture.ForegroundCount.Value.Should().BeLessThanOrEqualTo(capture.ExtractedCount.Value);
        capture.View.Minutiae.Should().NotBeEmpty();
        capture.View.Minutiae.Should().HaveCountLessThanOrEqualTo(128);
        capture
            .View.Minutiae.Should()
            .OnlyContain(point =>
                capture.Processing.Value.Artifact.Mask.Inner[(point.Y * 800) + point.X] != 0
            );
    }

    [Test]
    public void ActualEmptyPlatenFailsBeforeFeatureExtraction() =>
        ProcessingContractFixtures
            .Capture(
                Processor(),
                ProcessingFrameFixtures.NormalizedFrame(
                    ProcessingFrameFixtures.EmptyHardwareFrame()
                ),
                FingerPosition.LeftIndex
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Capture.NoFinger");

    [Test]
    public void TwoSeparatedFingerRegionsAreNotSilentlyLabeledAsOneFinger()
    {
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        foreach (var left in new[] { 100, 500 })
            for (var y = 200; y < 440; y++)
            for (var x = left; x < left + 160; x++)
                pixels[(y * 800) + x] = x % 8 < 4 ? (byte)40 : (byte)100;
        ProcessingContractFixtures
            .Capture(
                Processor(),
                ProcessingFrameFixtures.NormalizedFrame(pixels),
                FingerPosition.LeftIndex
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Capture.MultipleFingers");
    }

    private ICaptureProcessor Processor() => host.Services.GetRequiredService<ICaptureProcessor>();
}
