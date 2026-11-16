// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using System.Diagnostics;
using AwesomeAssertions;
using NUnit.Framework;

/// <summary>Opt-in warmed local throughput measurements using an actual empty-platen raw frame.</summary>
[TestFixture]
public sealed class Fs50ResamplerPerformanceTests
{
    /// <summary>Measures correction separately from USB, contact analysis and UI rendering.</summary>
    /// <param name="hardwareMap">Selects actual device calibration instead of the canonical synthetic calibration.</param>
    [TestCase(false)]
    [TestCase(true)]
    [Explicit("Local performance budget; run on an otherwise idle development host.")]
    public void WarmedCorrectionFitsTheLivePreviewBudget(bool hardwareMap)
    {
        using var fixture = Fs50GeometryFixture.Load();
        var map = hardwareMap
            ? HardwareVectorFixture.Map()
            : Fs50GeometryMap.Create(Fs50GeometryFixture.Parse(fixture.RootElement).Value).Value;
        var raw = HardwareVectorFixture.Image(200, "raw");
        for (var warmup = 0; warmup < 4; warmup++)
            Fs50Resampler.Correct(raw, map).IsSuccess.Should().BeTrue();
        var milliseconds = new double[12];
        for (var sample = 0; sample < milliseconds.Length; sample++)
        {
            var started = Stopwatch.GetTimestamp();
            var corrected = Fs50Resampler.Correct(raw, map);
            milliseconds[sample] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            corrected.IsSuccess.Should().BeTrue();
        }
        Array.Sort(milliseconds);
        var median = (milliseconds[5] + milliseconds[6]) / 2;
        TestContext.Out.WriteLine(
            $"Map={(hardwareMap ? "hardware" : "canonical")}; median={median:F3} ms; min={milliseconds[0]:F3} ms; max={milliseconds[^1]:F3} ms"
        );
        median
            .Should()
            .BeLessThan(75, "correction must leave time for USB, contact analysis and display");
    }
}
