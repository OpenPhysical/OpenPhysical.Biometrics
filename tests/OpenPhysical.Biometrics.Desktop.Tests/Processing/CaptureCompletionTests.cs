// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;

[TestFixture]
internal sealed class CaptureCompletionTests
{
    [Test]
    public void CompletionReusesExtractionAndMatchesTheNormalCanonicalOutputExactly()
    {
        using var services = new ScoringTestServices();
        var selection = Select(services);
        var prepared = services.Processor.ExtractPrepared(selection.Selected, default).Value;
        var completed = services.Processor.CompleteCapture(
            selection,
            prepared,
            FingerPosition.RightIndex,
            default,
            default
        );
        completed.Should().Succeed();
        services.Counts.Should().Be((1, 1, 1));
        var research = EvaluationCapture
            .Create(
                selection.SelectedArtifact.OriginalFrame,
                prepared,
                selection.SelectedArtifact.Mask.Inner,
                selection.Selected.Processed.Evidence.Nfiq2
            )
            .Value;
        Compare(research, completed.Value);
        var actual = services
            .Processor.Extract(selection, FingerPosition.RightIndex, default, default)
            .Value;
        Compare(completed.Value, actual);
        services.Counts.Should().Be((1, 1, 2));
    }

    [Test]
    public void CompletionRejectsAnExtractionOfDifferentPixelsWithoutNativeWork()
    {
        using var services = new ScoringTestServices();
        var first = Select(services);
        var changed = Select(services, 5);
        var foreign = services.Processor.ExtractPrepared(changed.Selected, default).Value;
        var before = services.Counts;
        services
            .Processor.CompleteCapture(first, foreign, FingerPosition.RightIndex, default, default)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Capture.QualityEvidenceMismatch");
        services.Counts.Should().Be(before);
    }

    private static CaptureBurstSelection Select(ScoringTestServices services, int brightness = 0) =>
        services
            .Processor.EvaluateFrame(
                ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger()),
                ProcessingContractFixtures.Request(brightness),
                default
            )
            .Value;

    private static bool Compare(CapturedFingerprint completed, CapturedFingerprint actual)
    {
        var a = UnsignedTemplateService.Create([completed], new(0x0103, 0, 0)).Value;
        var b = UnsignedTemplateService.Create([actual], new(0x0103, 0, 0)).Value;
        Incits378Codec.Write(a).Value.Should().Equal(Incits378Codec.Write(b).Value);
        var left = a.ToMatchingTemplate(FingerPosition.RightIndex, 0).Value;
        var right = b.ToMatchingTemplate(FingerPosition.RightIndex, 0).Value;
        left.Minutiae.Select(p => (p.X, p.Y, p.Angle, p.Quality, p.Type))
            .Should()
            .Equal(right.Minutiae.Select(p => (p.X, p.Y, p.Angle, p.Quality, p.Type)));
        return true;
    }
}
