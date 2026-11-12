// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Diagnostics;
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class CapturePreviewPerformanceTests
{
    [AvaloniaTest]
    [Explicit("Warmed target-machine public preview and live-tone feedback measurement.")]
    public async Task QualifiedPreviewAndLiveToneFeedbackDoNotRunNativeFingerprintProcessing()
    {
        await using var rig = new ProcessingCaptureRig();
        var pixels = ProcessingFrameFixtures.PublicFinger();
        var pending = await rig.StartAsync();
        var preview = await MeasurePreviewAsync(rig, pending, pixels);
        var original = rig.View.LiveFrame.Value.Frame;
        var feedback = await MeasureToneAsync(rig);
        rig.View.LiveFrame.Value.Frame.Should().BeSameAs(original);
        rig.View.Captured.HasNoValue.Should().BeTrue();
        rig.Services.Counts.Should().Be((0, 0, 0));
        TestContext.Out.WriteLine(
            $"Public preview/UI acknowledgement: median {Median(preview):F2} ms; p95 {P95(preview):F2} ms; "
                + $"unpaced software rate {1000 / preview.Average():F2} frames/s. "
                + $"Live tone/bitmap feedback: median {Median(feedback):F2} ms; p95 {P95(feedback):F2} ms. "
                + "12 warmed samples per path; 0 NFIQ 1, 0 NFIQ 2, 0 extraction calls. "
                + "Scripted acquisition excludes USB and physical display scanout."
        );
        P95(feedback).Should().BeLessThan(100, "live image controls must respond within 100 ms");
    }

    private static async Task<double[]> MeasurePreviewAsync(
        ProcessingCaptureRig rig,
        ControlledCaptureRequest pending,
        byte[] pixels
    )
    {
        var samples = new double[12];
        var sourceDigests = new HashSet<string>();
        for (var index = 0; index < samples.Length + 3; index++)
        {
            var observation = pixels.ToArray();
            observation[index] = 254;
            rig.Clock.Advance(TimeSpan.FromMilliseconds(250));
            var started = Stopwatch.GetTimestamp();
            pending.Complete(observation).Should().BeTrue();
            pending = await rig.Driver.NextAsync();
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            AssertQualifiedPresentation(rig.View);
            sourceDigests.Add(rig.View.LiveFrame.Value.Processing.Value.OriginalDigest);
            if (index >= 3)
                samples[index - 3] = elapsed;
        }
        sourceDigests.Count.Should().Be(samples.Length + 3);
        return samples;
    }

    private static async Task<double[]> MeasureToneAsync(ProcessingCaptureRig rig)
    {
        var samples = new double[12];
        for (var index = 0; index < samples.Length + 3; index++)
        {
            var brightness = index + 1;
            var started = Stopwatch.GetTimestamp();
            rig.View.ConfigureImageAdjustments(
                    ImageAdjustmentSettings.Create(false, brightness, 0).Value,
                    CapturePolicySettings.Default
                )
                .Should()
                .Succeed();
            await CaptureWorkflowWait.UntilAsync(
                rig.View,
                () => ToneIsDisplayed(rig.View, brightness)
            );
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            AssertQualifiedPresentation(rig.View);
            if (index >= 3)
                samples[index - 3] = elapsed;
        }
        return samples;
    }

    private static bool AssertQualifiedPresentation(CaptureViewModel view)
    {
        view.LiveFrame.HasValue.Should().BeTrue();
        var displayed = view.LiveFrame.Value;
        displayed.WholeContactProof.HasValue.Should().BeTrue();
        var proof = displayed.WholeContactProof.Value;
        proof.Qualified.Should().BeTrue();
        proof.Record.Identity.SourceDigest.Should().Be(displayed.Processing.Value.OriginalDigest);
        proof.Record.Identity.Sequence.Should().Be(displayed.Frame.Sequence);
        proof.Record.Identity.CapturedAt.Should().Be(displayed.Frame.CapturedAt);
        view.Scene.Value.GrayPixels.Should().Equal(displayed.Processing.Value.Pixels);
        view.CaptureCommand.CanExecute(default).Should().BeTrue();
        return true;
    }

    private static bool ToneIsDisplayed(CaptureViewModel view, int brightness) =>
        view.LiveFrame.HasValue
        && view.LiveFrame.Value.Processing.HasValue
        && view.LiveFrame.Value.Processing.Value.Policy.Brightness == brightness
        && view.Scene.HasValue
        && view.Scene.Value.GrayPixels.AsSpan()
            .SequenceEqual(view.LiveFrame.Value.Processing.Value.Pixels.AsSpan());

    private static double Median(double[] samples)
    {
        var ordered = samples.Order().ToArray();
        return (ordered[5] + ordered[6]) / 2;
    }

    private static double P95(double[] samples)
    {
        var ordered = samples.Order().ToArray();
        return ordered[(int)Math.Ceiling(ordered.Length * 0.95) - 1];
    }
}
