// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class CaptureScoringTests
{
    [Test]
    public void FrozenBurstScoresEachCandidateOnceThenExtractsOnlyTheSelectedImage()
    {
        using var services = new ScoringTestServices();
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        var candidates = ScoringTestServices.Candidates(frame, 3);
        var selection = services
            .Processor.EvaluateBurst(
                candidates,
                ProcessingContractFixtures.Request(revision: 7),
                FingerprintQualitySelectionPolicy.Default,
                CancellationToken.None
            )
            .Value;
        services.Nfiq1.Calls.Should().Be(3);
        services.Nfiq2.Calls.Should().Be(3);
        services.Engine.Extractions.Should().Be(0);
        var capture = services
            .Processor.Extract(
                selection,
                FingerPosition.RightIndex,
                default,
                CancellationToken.None
            )
            .Value;
        services.Nfiq1.Calls.Should().Be(3);
        services.Nfiq2.Calls.Should().Be(3);
        services.Engine.Extractions.Should().Be(1);
        services.Engine.LastPixels.Should().Equal(selection.SelectedArtifact.Pixels);
        capture.Frame.Should().BeSameAs(selection.SelectedArtifact.OriginalFrame);
        capture.Processing.Value.ProcessingRevision.Should().Be(7);
        capture.Processing.Value.Candidates.Should().HaveCount(3);
        capture.ExtractorVersion.Value.Should().Be("NBIS 5.0.0 MINDTCT V2");
        UnsignedTemplateServiceCheck(capture);
        services.Engine.Extractions.Should().Be(1);
        services.Nfiq1.Calls.Should().Be(3);
        services.Nfiq2.Calls.Should().Be(3);
    }

    [Test]
    public void FrozenAdjustmentReusesOriginalScoresAndExtractsTheNewExactProcessingPixels()
    {
        using var services = new ScoringTestServices();
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        var first = services
            .Processor.EvaluateArtifact(
                ProcessingContractFixtures.Prepared(services, frame, 0, 1),
                FingerprintQualitySelectionPolicy.Default,
                CancellationToken.None
            )
            .Value;
        var captured = services
            .Processor.Extract(first, FingerPosition.RightIndex, default, CancellationToken.None)
            .Value;
        var revised = services
            .Processor.EvaluateArtifact(
                ProcessingContractFixtures.Prepared(services, frame, 5, 2),
                FingerprintQualitySelectionPolicy.Default,
                CancellationToken.None
            )
            .Value;
        var result = services
            .Processor.Extract(
                revised,
                captured.Finger,
                captured.Processing.Value.OriginalAssessment,
                CancellationToken.None
            )
            .Value;
        services.Counts.Should().Be((2, 2, 2));
        services.Engine.LastPixels.Should().Equal(revised.SelectedArtifact.Pixels);
        result
            .Processing.Value.OriginalAssessment.Should()
            .BeSameAs(captured.Processing.Value.OriginalAssessment);
        result.Frame.Should().BeSameAs(frame);
    }

    [Test]
    public void CancelledEvaluationDoesNotCallEitherNativeQualityEngineOrExtraction()
    {
        using var services = new ScoringTestServices();
        var artifact = ProcessingContractFixtures.Prepared(
            services,
            ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger()),
            0,
            1
        );
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        services
            .Processor.EvaluateArtifact(
                artifact,
                FingerprintQualitySelectionPolicy.Default,
                cancelled.Token
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.Cancelled);
        services.Nfiq1.Calls.Should().Be(0);
        services.Nfiq2.Calls.Should().Be(0);
        services.Engine.Extractions.Should().Be(0);
    }

    private static bool UnsignedTemplateServiceCheck(CapturedFingerprint capture)
    {
        UnsignedTemplateService.Create([capture], new(0x0103, 0, 0)).Should().Succeed();
        return true;
    }
}
