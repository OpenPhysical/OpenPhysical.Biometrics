// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using System.Diagnostics;
using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class CapturePipelinePerformanceTests
{
    [Test]
    [Explicit("Warmed target-machine performance and native-call accounting.")]
    public void FiveCandidatePipelineHasSubTwoSecondP95WithoutRepeatedQualityOrExtraction()
    {
        using var services = new ScoringTestServices();
        var frame = ProcessingFrameFixtures.NormalizedFrame(ProcessingFrameFixtures.PublicFinger());
        var candidates = IndependentFrames(frame);
        for (var warmup = 0; warmup < 3; warmup++)
            Process(services, candidates, warmup);
        var timings = new List<double>();
        for (var sample = 0; sample < 12; sample++)
        {
            var started = Stopwatch.GetTimestamp();
            Process(services, candidates, sample + 3);
            timings.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        timings.Sort();
        var p95 = timings[(int)Math.Ceiling(timings.Count * 0.95) - 1];
        services.Counts.Should().Be((75, 75, 15));
        TestContext.Out.WriteLine(
            $"Warmed five-frame backend: median {timings[6]:F1} ms; p95 {p95:F1} ms; 12 samples."
        );
        p95.Should()
            .BeLessThan(
                2000,
                "five candidates must be assessed, the unchanged original must reuse its assessment, and only the winner may be extracted"
            );
    }

    private static ImmutableArray<FingerCaptureCandidate> IndependentFrames(CaptureFrame frame) =>
        ScoringTestServices
            .Candidates(frame, 5)
            .Select(
                (candidate, index) =>
                {
                    var pixels = candidate.Frame.Pixels.ToArray();
                    pixels[0] = (byte)(250 + index);
                    return candidate with
                    {
                        Frame = CaptureFrame
                            .Create(
                                pixels,
                                candidate.Frame.Sequence,
                                candidate.Frame.CapturedAt,
                                candidate.Frame.Provenance
                            )
                            .Value,
                    };
                }
            )
            .ToImmutableArray();

    private static bool Process(
        ScoringTestServices services,
        ImmutableArray<FingerCaptureCandidate> candidates,
        long revision
    )
    {
        var policy = CapturePolicySettings.Default;
        var evaluated = services.Processor.EvaluateBurst(
            candidates,
            new(
                policy.Enhancement.For(ImageAdjustmentSettings.Default),
                policy.Evidence,
                policy.Contour,
                revision,
                CaptureProcessingInput.DeclaredOfflineImage
            ),
            FingerprintQualitySelectionPolicy.Default,
            CancellationToken.None
        );
        evaluated.IsSuccess.Should().BeTrue();
        var capture = services.Processor.Extract(
            evaluated.Value,
            FingerPosition.RightIndex,
            default,
            CancellationToken.None
        );
        capture.IsSuccess.Should().BeTrue();
        capture.Value.Processing.Value.ProcessingRevision.Should().Be(revision);
        capture.Value.Quality.Should().BeSameAs(evaluated.Value.Selected.Processed.Evidence.Nfiq1);
        capture
            .Value.Nfiq2.Value.Should()
            .BeSameAs(evaluated.Value.Selected.Processed.Evidence.Nfiq2.Value);
        return true;
    }
}
