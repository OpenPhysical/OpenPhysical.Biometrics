// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

/// <summary>Complete empty-platen frames preserve real USB acquisition and correction behavior.</summary>
[TestFixture]
public sealed class HardwareVectorParityTests
{
    /// <summary>Each actual exposure preserves all raw and corrected bytes and reports no contact.</summary>
    /// <param name="exposure">Actual volatile acquisition exposure.</param>
    [TestCase(0)]
    [TestCase(64)]
    [TestCase(128)]
    [TestCase(200)]
    [TestCase(255)]
    public void EmptyPlatenFramesMatchOriginalHashesAndCompleteIndependentCorrection(int exposure)
    {
        using var manifest = HardwareVectorFixture.Manifest();
        var expected = manifest
            .RootElement.GetProperty("empty")
            .EnumerateArray()
            .Single(frame => frame.GetProperty("exposure").GetInt32() == exposure);
        var raw = HardwareVectorFixture.Image(exposure, "raw");
        var golden = HardwareVectorFixture.Image(exposure, "corrected");
        raw.Should().HaveCount(1280 * 832);
        golden.Should().HaveCount(800 * 750);
        Fs50ResamplerParityTests
            .Hash(raw)
            .Should()
            .Be(expected.GetProperty("rawSha256").ToString());
        Fs50ResamplerParityTests
            .Hash(golden)
            .Should()
            .Be(expected.GetProperty("correctedSha256").ToString());
        var corrected = Fs50Resampler.Correct(raw, HardwareVectorFixture.Map());
        corrected.Should().Succeed();
        corrected.Value.Should().Equal(golden);
        Fs50ResamplerParityTests
            .Hash(raw)
            .Should()
            .Be(expected.GetProperty("rawSha256").ToString());
        var contact = Fs50ContactAnalysis.Analyze(corrected.Value.AsSpan());
        contact.Should().Succeed();
        contact.Value.Present.Should().BeFalse();
        contact.Value.Area.Should().Be(expected.GetProperty("contactArea").GetInt32());
        corrected.Value.Min().Should().Be(expected.GetProperty("minimum").GetByte());
        corrected.Value.Max().Should().Be(expected.GetProperty("maximum").GetByte());
    }

    /// <summary>Synthetic patterns independently checked with OpenCV preserve the real calibration.</summary>
    /// <param name="pattern">Deterministic source-image recipe.</param>
    [TestCase("horizontal-ramp")]
    [TestCase("vertical-ramp")]
    [TestCase("checkerboard")]
    public void SyntheticPatternsMatchCapturedIndependentOracleDigests(string pattern)
    {
        using var manifest = HardwareVectorFixture.Manifest();
        var expected = manifest
            .RootElement.GetProperty("synthetic")
            .EnumerateArray()
            .Single(frame => frame.GetProperty("pattern").ToString() == pattern);
        var raw = Fs50ResamplerParityTests.Generate(pattern, 0, rampDivisor: 1, checkerSize: 8);
        Fs50ResamplerParityTests
            .Hash(raw)
            .Should()
            .Be(expected.GetProperty("rawSha256").ToString());
        var corrected = Fs50Resampler.Correct(raw, HardwareVectorFixture.Map());
        corrected.Should().Succeed();
        Fs50ResamplerParityTests
            .Hash(corrected.Value.AsSpan())
            .Should()
            .Be(expected.GetProperty("correctedSha256").ToString());
    }
}
