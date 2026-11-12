// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

[TestFixture]
internal sealed class WholeContactProcessingTests
{
    [Test]
    public void MissingPhysicalQualificationCannotStartNativeProcessing()
    {
        using var services = new ScoringTestServices();
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        var request = new CaptureProcessingRequest(
            FingerprintPreprocessingPolicy.Create(false, contrast: 0).Value,
            Fs50EvidencePolicy.Default,
            Fs50ContactContourPolicy.Default,
            1,
            CaptureProcessingInput.LiveSensor([])
        );
        services
            .Processor.EvaluateFrame(frame, request, default)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Capture.WholeContactUnqualified");
        services.Counts.Should().Be((0, 0, 0));
    }

    [Test]
    public void DeclaredOfflineImageRetainsItsHonestRoleWithoutSensorQualification()
    {
        using var services = new ScoringTestServices();
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        var request = new CaptureProcessingRequest(
            FingerprintPreprocessingPolicy.Create(false, contrast: 0).Value,
            Fs50EvidencePolicy.Default,
            Fs50ContactContourPolicy.Default,
            1,
            CaptureProcessingInput.DeclaredOfflineImage
        );
        var prepared = services.Processor.PrepareFrame(frame, request, default).Value;
        prepared.InputRole.Should().Be(CaptureProcessingInputRole.DeclaredOfflineImage);
        prepared.WholeContactProof.HasNoValue.Should().BeTrue();
        services.Counts.Should().Be((0, 0, 0));
    }

    [AvaloniaTest]
    public async Task RecordedMetadataCannotAuthorizeReprocessingAsPhysicalSensorInput()
    {
        await using var rig = new ProcessingCaptureRig();
        rig.Driver.Available = false;
        var recorded = SyntheticRecordedQualificationFixtures.Capture();
        recorded.Processing.Value.OriginalQualification.HasValue.Should().BeTrue();
        var adjusted = await rig.View.AdjustAcceptedAsync(recorded);
        adjusted.Should().Fail().Which.Code.Should().Be("Capture.WholeContactUnqualified");
        rig.Services.Counts.Should().Be((0, 0, 0));
    }
}
