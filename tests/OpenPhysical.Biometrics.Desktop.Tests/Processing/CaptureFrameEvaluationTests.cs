// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

[TestFixture]
internal sealed class CaptureFrameEvaluationTests
{
    [Test]
    public void FrozenManualFrameIsPreparedWithoutInventedBurstMeasurements()
    {
        using var services = new ScoringTestServices();
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        ICaptureProcessor processor = services.Processor;
        var selected = processor
            .EvaluateFrame(
                frame,
                ProcessingContractFixtures.Request(revision: 17),
                CancellationToken.None
            )
            .Value;
        selected.Reason.Should().Be(CaptureSelectionReason.Manual);
        selected.Candidates.Should().HaveCount(1);
        selected.Selected.Source.HasNoValue.Should().BeTrue();
        selected.SelectedArtifact.OriginalFrame.Should().BeSameAs(frame);
        selected.SelectedArtifact.ProcessingRevision.Should().Be(17);
        selected.SelectedArtifact.Pixels.Should().Equal(frame.Pixels);
        selected
            .Selected.ContactDiagnostics.SourceDigest.Should()
            .Be(selected.SelectedArtifact.OriginalDigest);
        services.Counts.Should().Be((1, 1, 0));
    }

    [Test]
    public void RejectedFrozenContactNeverReachesNativeQualityOrExtraction()
    {
        using var services = new ScoringTestServices();
        var frame = ProcessingFrameFixtures.NormalizedFrame(
            ProcessingFrameFixtures.PaleRidgeContact()
        );
        services
            .Processor.EvaluateFrame(
                frame,
                ProcessingContractFixtures.Request(),
                CancellationToken.None
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Capture.ContactTooLight");
        services.Counts.Should().Be((0, 0, 0));
    }

    [Test]
    public void CancellationBeforePreparationCannotStartNativeWork()
    {
        using var services = new ScoringTestServices();
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        services
            .Processor.EvaluateFrame(frame, ProcessingContractFixtures.Request(), cancelled.Token)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.Cancelled);
        services.Counts.Should().Be((0, 0, 0));
    }
}
