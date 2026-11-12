// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using System.Text.Json;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

/// <summary>Preserves actual scanner metadata and independently reviewed map observations.</summary>
[TestFixture]
public sealed class HardwareVectorMetadataTests
{
    /// <summary>The fixture retains exactly the descriptor, controls, and twice-read calibration.</summary>
    /// <param name="file">Captured metadata file.</param>
    /// <param name="length">Complete expected byte count.</param>
    /// <param name="hashField">Original capture manifest digest field.</param>
    [TestCase("descriptor.bin", 64, "descriptorSha256")]
    [TestCase("controls.bin", 64, "controlsSha256")]
    [TestCase("calibration.bin", 4352, "calibrationSha256")]
    public void MetadataMatchesOriginalCaptureDigest(string file, int length, string hashField)
    {
        using var manifest = HardwareVectorFixture.Manifest();
        var bytes = HardwareVectorFixture.Metadata(file);
        bytes.Should().HaveCount(length);
        Fs50ResamplerParityTests
            .Hash(bytes)
            .Should()
            .Be(manifest.RootElement.GetProperty(hashField).ToString());
        manifest.RootElement.GetProperty("calibrationReadPasses").GetInt32().Should().Be(2);
    }

    /// <summary>The map preserves nine observed coordinates across the entire platen.</summary>
    [Test]
    public void FactoryMapMatchesAllNineHardwareObservations()
    {
        using var manifest = HardwareVectorFixture.Manifest();
        var calibration = Fs50Calibration.Parse(
            HardwareVectorFixture.Metadata("descriptor.bin"),
            HardwareVectorFixture.Metadata("controls.bin"),
            HardwareVectorFixture.Metadata("calibration.bin")
        );
        calibration.Should().Succeed();
        var map = calibration.Bind(Fs50GeometryMap.Create);
        map.Should().Succeed();
        var points = manifest.RootElement.GetProperty("points");
        points.GetArrayLength().Should().Be(9);
        foreach (var point in points.EnumerateArray())
        {
            var actual = map.Value.GetPoint(
                point.GetProperty("x").GetInt32(),
                point.GetProperty("y").GetInt32()
            );
            actual.Should().Succeed();
            actual.Value.X.Should().Be(point.GetProperty("sourceX").GetSingle());
            actual.Value.Y.Should().Be(point.GetProperty("sourceY").GetSingle());
        }
        Fs50Resampler.ProfileId.Should().Be(manifest.RootElement.GetProperty("profile").ToString());
    }

    /// <summary>The retained oracle receipt covers every real and synthetic complete image.</summary>
    [Test]
    public void IndependentOracleReceiptCoversAllEightFrames()
    {
        using var verification = JsonDocument.Parse(
            File.ReadAllText(
                Path.Combine(HardwareVectorFixture.Directory, "independent-verification.json")
            )
        );
        var results = verification.RootElement.GetProperty("results");
        results.GetArrayLength().Should().Be(8);
        foreach (var result in results.EnumerateArray())
        {
            var property = result.TryGetProperty("exposure", out _)
                ? "independent_corrected_bytes_match"
                : "independent_opencv_match";
            result.GetProperty(property).GetBoolean().Should().BeTrue();
        }
    }
}
