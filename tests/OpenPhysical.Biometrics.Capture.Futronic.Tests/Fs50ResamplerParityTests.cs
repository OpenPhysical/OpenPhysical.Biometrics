// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using System.Buffers.Binary;
using System.Text.Json;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using Org.BouncyCastle.Crypto.Digests;

/// <summary>Full-frame synthetic byte parity against the pinned development-only oracle.</summary>
[TestFixture]
public sealed class Fs50ResamplerParityTests
{
    /// <summary>Every pixel, not only a smooth ramp, contributes to the independent golden hash.</summary>
    /// <param name="frameIndex">Synthetic frame recipe in the immutable oracle fixture.</param>
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    public void CompleteSyntheticFramesMatchPinnedOracle(int frameIndex)
    {
        using var geometryFixture = Fs50GeometryFixture.Load();
        using var oracle = LoadOracle();
        var frame = oracle.RootElement.GetProperty("frames")[frameIndex];
        var geometry = frame.TryGetProperty("geometry", out var overrideGeometry)
            ? overrideGeometry
            : geometryFixture.RootElement;
        var mapResult = Fs50GeometryMap.Create(Fs50GeometryFixture.Parse(geometry).Value);
        mapResult.Should().Succeed();
        var map = mapResult.Value;
        if (frame.TryGetProperty("mapBounds", out var bounds))
        {
            HasExpectedBounds(map, bounds).Should().BeTrue();
        }
        var source = Generate(
            frame.GetProperty("kind").ToString(),
            frame.GetProperty("seed").GetUInt32()
        );
        Hash(source).Should().Be(frame.GetProperty("rawSha256").ToString());
        var corrected = Fs50Resampler.Correct(source, map);
        corrected.Should().Succeed();
        Hash(corrected.Value.AsSpan()).Should().Be(frame.GetProperty("correctedSha256").ToString());
        Hash(source).Should().Be(frame.GetProperty("rawSha256").ToString());
        foreach (var sample in frame.GetProperty("samples").EnumerateArray())
        {
            corrected
                .Value[sample.GetProperty("offset").GetInt32()]
                .Should()
                .Be((byte)sample.GetProperty("value").GetInt32());
        }
    }

    /// <summary>Preserves the factory map independently of byte interpolation changes.</summary>
    [Test]
    public void FactoryCoordinateHashesAndFractionsMatchOracle()
    {
        using var fixture = Fs50GeometryFixture.Load();
        using var oracle = LoadOracle();
        var map = Fs50GeometryMap
            .Create(Fs50GeometryFixture.Parse(fixture.RootElement).Value)
            .Value;
        var xBytes = new byte[800 * 750 * 4];
        var yBytes = new byte[xBytes.Length];
        var allFractionsExact = true;
        for (var y = 0; y < 750; y++)
        {
            for (var x = 0; x < 800; x++)
            {
                var point = map.GetPoint(x, y).Value;
                var offset = ((y * 800) + x) * 4;
                BinaryPrimitives.WriteSingleLittleEndian(xBytes.AsSpan(offset, 4), point.X);
                BinaryPrimitives.WriteSingleLittleEndian(yBytes.AsSpan(offset, 4), point.Y);
                allFractionsExact &=
                    point.X * 128 == MathF.Truncate(point.X * 128)
                    && point.Y * 128 == MathF.Truncate(point.Y * 128);
            }
        }

        allFractionsExact.Should().BeTrue();
        Hash(xBytes).Should().Be(oracle.RootElement.GetProperty("mapXSha256").ToString());
        Hash(yBytes).Should().Be(oracle.RootElement.GetProperty("mapYSha256").ToString());
        Fs50Resampler.ProfileId.Should().Be(oracle.RootElement.GetProperty("profileId").ToString());
        Fs50Resampler
            .CoefficientsSha256.Should()
            .Be(oracle.RootElement.GetProperty("coefficientBitsLittleEndianSha256").ToString());
    }

    private static bool HasExpectedBounds(Fs50GeometryMap map, JsonElement bounds)
    {
        var first = map.GetPoint(0, 0).Value;
        var last = map.GetPoint(799, 749).Value;
        return first.X == bounds.GetProperty("minX").GetSingle()
            && first.Y == bounds.GetProperty("minY").GetSingle()
            && last.X == bounds.GetProperty("maxX").GetSingle()
            && last.Y == bounds.GetProperty("maxY").GetSingle();
    }

    private static byte[] Generate(string kind, uint seed)
    {
        var bytes = new byte[1280 * 832];
        uint state = seed;
        for (var index = 0; index < bytes.Length; index++)
        {
            var x = index % 1280;
            var y = index / 1280;
            if (kind == "xorshift32")
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                bytes[index] = (byte)(state >> 24);
            }
            else
            {
                bytes[index] = kind switch
                {
                    "horizontal-ramp" => (byte)(x / 5),
                    "vertical-ramp" => (byte)(y % 256),
                    "checkerboard" => (byte)(((x + y) & 1) * 255),
                    _ => 0,
                };
            }
        }

        return bytes;
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        var digest = new Sha256Digest();
        digest.BlockUpdate(bytes);
        var result = new byte[digest.GetDigestSize()];
        _ = digest.DoFinal(result);
        return Convert.ToHexStringLower(result);
    }

    private static JsonDocument LoadOracle() =>
        JsonDocument.Parse(
            File.ReadAllText(
                Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    "Fixtures",
                    "fs50-resampler-half128-v1.json"
                )
            )
        );
}
