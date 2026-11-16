// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using System.Buffers.Binary;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

/// <summary>Factory mapping parity and invalid-calibration boundaries.</summary>
[TestFixture]
public sealed class Fs50GeometryTests
{
    /// <summary>Checks an independently obtained vendor ramp.</summary>
    [Test]
    public void FactoryMapMatchesIndependentVendorRampSamples()
    {
        using var fixture = Fs50GeometryFixture.Load();
        var calibration = Fs50GeometryFixture.Parse(fixture.RootElement);
        calibration.Should().Succeed();
        var map = Fs50GeometryMap.Create(calibration.Value);
        map.Should().Succeed();
        var raw = new byte[1280 * 832];
        for (var i = 0; i < raw.Length; i++)
        {
            raw[i] = (byte)((i % 1280) / 5);
        }

        var result = Fs50Resampler.Correct(raw, map.Value);
        result.Should().Succeed();
        result.Value.Length.Should().Be(800 * 750);
        var expected = fixture
            .RootElement.GetProperty("firstHorizontalRampSamples")
            .EnumerateArray()
            .Select(x => (byte)(255 - x.GetInt32()));
        result.Value.Take(20).Should().Equal(expected);
        raw[1280].Should().Be(0);
        map.Value.GetPoint(0, 0).Value.X.Should().BeGreaterThan(0);
    }

    /// <summary>Preserves constant pixels and rejects short frames.</summary>
    [Test]
    public void ConstantImageIsInvertedWithoutChangingItsInput()
    {
        using var fixture = Fs50GeometryFixture.Load();
        var map = Fs50GeometryMap
            .Create(Fs50GeometryFixture.Parse(fixture.RootElement).Value)
            .Value;
        var input = Enumerable.Repeat((byte)87, 1280 * 832).ToArray();
        var result = Fs50Resampler.Correct(input, map);
        result.Should().Succeed();
        result.Value.All(x => x == 168).Should().BeTrue();
        input.All(x => x == 87).Should().BeTrue();
        Fs50Resampler
            .Correct(input.AsSpan(1), map)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.TransferIncomplete);
        map.GetPoint(800, 0)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.InvalidArgument);
    }

    /// <summary>Rejects unsupported orientation and correction versions.</summary>
    /// <param name="offset">Descriptor byte to alter.</param>
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(38)]
    public void OrientationAndVersionAreNotGuessed(int offset)
    {
        using var fixture = Fs50GeometryFixture.Load();
        var descriptor = Convert.FromHexString(
            fixture.RootElement.GetProperty("descriptorHex").ToString()
        );
        descriptor[offset] ^= 1;
        Fs50Descriptor
            .Validate(descriptor)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.UnsupportedGeometry);
    }

    /// <summary>Rejects folded and out-of-sensor mappings.</summary>
    /// <param name="replacement">Invalid scale coefficient.</param>
    [TestCase(-15444)]
    [TestCase(1000000)]
    [TestCase(0)]
    public void FoldedOrOutOfBoundsMapsAreRejected(int replacement)
    {
        using var fixture = Fs50GeometryFixture.Load();
        var bytes = Fs50GeometryFixture.CalibrationBytes(fixture.RootElement);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), replacement);
        var calibration = Fs50Calibration.Parse(
            Fs50GeometryFixture.Descriptor(fixture.RootElement),
            Fs50GeometryFixture.Controls(fixture.RootElement),
            bytes
        );
        calibration.Should().Succeed();
        Fs50GeometryMap
            .Create(calibration.Value)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.CalibrationInvalid);
    }

    /// <summary>Bounds coefficient and crop inputs before map allocation.</summary>
    [Test]
    public void TruncatedCalibrationAndInvalidCoefficientsFailBeforeMapping()
    {
        using var fixture = Fs50GeometryFixture.Load();
        var descriptor = Fs50GeometryFixture.Descriptor(fixture.RootElement);
        var controls = Fs50GeometryFixture.Controls(fixture.RootElement);
        Fs50Calibration
            .Parse(descriptor, controls, Array.Empty<byte>())
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.CalibrationInvalid);
        var bytes = Fs50GeometryFixture.CalibrationBytes(fixture.RootElement);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), int.MinValue);
        Fs50Calibration
            .Parse(descriptor, controls, bytes)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.CalibrationInvalid);
        controls[4] = 83;
        Fs50Calibration
            .Parse(descriptor, controls, Fs50GeometryFixture.CalibrationBytes(fixture.RootElement))
            .Should()
            .Fail();
    }

    /// <summary>Prevents callers from changing retained provenance.</summary>
    [Test]
    public void CalibrationCopiesItsProvenanceBytes()
    {
        using var fixture = Fs50GeometryFixture.Load();
        var bytes = Fs50GeometryFixture.CalibrationBytes(fixture.RootElement);
        var value = Fs50Calibration
            .Parse(
                Fs50GeometryFixture.Descriptor(fixture.RootElement),
                Fs50GeometryFixture.Controls(fixture.RootElement),
                bytes
            )
            .Value;
        bytes[16] ^= 255;
        value.Bytes[16].Should().NotBe(bytes[16]);
    }

    /// <summary>A positive determinant alone must not admit a rotated image.</summary>
    [Test]
    public void BothAxesReversedAreRejectedDespitePositiveDeterminant()
    {
        using var fixture = Fs50GeometryFixture.Load();
        var bytes = new byte[4352];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), -10000);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(48), -10000);
        var calibration = Fs50Calibration.Parse(
            Fs50GeometryFixture.Descriptor(fixture.RootElement),
            Fs50GeometryFixture.Controls(fixture.RootElement),
            bytes
        );
        calibration.Should().Succeed();
        Fs50GeometryMap
            .Create(calibration.Value)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(CaptureErrorCodes.CalibrationInvalid);
    }
}
