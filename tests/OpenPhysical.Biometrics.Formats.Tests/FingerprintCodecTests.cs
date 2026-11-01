// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Tests;

using System.Buffers.Binary;
using System.Collections.Immutable;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Exercises INCITS 378 wire layouts and trust boundaries independently of image processing.</summary>
[TestFixture]
public sealed class FingerprintCodecTests
{
    /// <summary>Checks the independent FMR wire vector and coordinate/angle units.</summary>
    [Test]
    public void FingerprintWireVectorRoundTrips()
    {
        var bytes = CodecTestData.FingerprintBytes();
        var parsed = Incits378Codec.Parse(bytes);
        parsed.Should().Succeed();
        var record = parsed.Value.Record;
        using (new AssertionScope())
        {
            record.XResolution.Should().Be(197);
            record.Views.Single().FingerPosition.Should().Be(2);
            record
                .Views.Single()
                .Minutiae.Single()
                .Should()
                .Be(new FingerprintMinutia(1, 10, 20, 45, 100));
            Incits378Codec.Write(record).Value.Should().Equal(bytes);
        }
        Array.Clear(bytes);
        record.Views[0].Minutiae[0].X.Should().Be(10);
    }

    /// <summary>Preserves unknown extended blocks and derives their inclusive lengths.</summary>
    [Test]
    public void UnknownExtendedBlocksPreserveTheirOriginalBytes()
    {
        var original = CodecTestData.Fingerprint();
        var view = original.Views[0] with { ExtendedBlocks = [new(0x4321, [0xAA, 0x55])] };
        var bytes = Incits378Codec.Write(original with { Views = [view] }).Value;
        var parsed = Incits378Codec.Parse(bytes);
        parsed.Should().Succeed();
        parsed
            .Value.Record.Views[0]
            .ExtendedBlocks[0]
            .Data.Should()
            .Equal(new byte[] { 0xAA, 0x55 });
        Incits378Codec.Write(parsed.Value.Record).Value.Should().Equal(bytes);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(bytes.Length - 4), 3);
        Incits378Codec.Parse(bytes).Should().Fail().Which.Code.Should().Be("ExtendedBlockLength");
    }

    /// <summary>Preserves the opposite core and delta angle-presence flags.</summary>
    [Test]
    public void CoreAndDeltaAngleFlagsFollowTheirOwnConventions()
    {
        var original = CodecTestData.Fingerprint();
        // One core with an angle, then one delta with three angles. Core uses
        // flag 01 for angles; the delta uses flag 00 for its three directions.
        var data = ImmutableArray.Create<byte>(
            0x41,
            0,
            10,
            0,
            20,
            45,
            0x01,
            0,
            30,
            0,
            40,
            0,
            45,
            90
        );
        var view = original.Views[0] with { ExtendedBlocks = [new(2, data)] };
        var bytes = Incits378Codec.Write(original with { Views = [view] }).Value;
        var parsed = Incits378Codec.Parse(bytes);
        parsed.Should().Succeed();
        parsed.Value.Record.Views[0].ExtendedBlocks[0].Data.Should().Equal(data);
        bytes[^1] = 180;
        Incits378Codec.Parse(bytes).Should().Fail().Which.Code.Should().Be("CoreDeltaAngle");
    }

    /// <summary>Exercises the six-byte record-length encoding beyond the short-length boundary.</summary>
    [Test]
    public void ExtendedRecordLengthIsComputedFromAllViews()
    {
        var original = CodecTestData.Fingerprint();
        var block = new FingerprintExtendedBlock(0x4321, ImmutableArray.Create(new byte[35000]));
        var first = original.Views[0] with { ExtendedBlocks = [block] };
        var second = first with { FingerPosition = 7 };
        var bytes = Incits378Codec.Write(original with { Views = [first, second] }).Value;
        using (new AssertionScope())
        {
            BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(8)).Should().Be(0);
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(10)).Should().Be((uint)bytes.Length);
            Incits378Codec.Parse(bytes).Should().Succeed();
        }
    }

    /// <summary>Rejects reserved bits and values that would otherwise change minutia interpretation.</summary>
    /// <param name="offset">Byte to corrupt in the independent wire vector.</param>
    /// <param name="value">Invalid replacement value.</param>
    [TestCase(25, 1)]
    [TestCase(30, 0xC0)]
    [TestCase(32, 0xC0)]
    [TestCase(34, 180)]
    [TestCase(35, 101)]
    public void InvalidMinutiaAndReservedValuesAreRejected(int offset, byte value)
    {
        var bytes = CodecTestData.FingerprintBytes();
        bytes[offset] = value;
        Incits378Codec.Parse(bytes).Should().Fail();
    }

    /// <summary>Keeps inspection contents private until an explicit sensitive-value request.</summary>
    [Test]
    public void InspectionValuesRequireExplicitOptIn()
    {
        var bytes = CodecTestData.FingerprintBytes();
        var ordinary = Incits378Codec.Parse(bytes).Value;
        var sensitive = Incits378Codec.Parse(bytes, new(IncludeSensitiveValues: true)).Value;
        ordinary.Inspection.Fields.All(field => field.Value.HasNoValue).Should().BeTrue();
        sensitive
            .Inspection.Fields.Any(field => field.Value.HasValue && field.Value.Value == "400A")
            .Should()
            .BeTrue();
        var point = ordinary.Inspection.Fields.Single(field =>
            field.Name.EndsWith("TypeAndX", StringComparison.Ordinal)
        );
        (point.Offset, point.Length).Should().Be((30, 2));
    }

    /// <summary>Reports malformed fingerprint version and extension framing.</summary>
    /// <param name="version">True corrupts the version; false corrupts the extension length.</param>
    [TestCase(true)]
    [TestCase(false)]
    public void FingerprintFramingRejectsUnsupportedVersionAndTruncatedExtensions(bool version)
    {
        var bytes = CodecTestData.FingerprintBytes();
        if (version)
            bytes[5] = (byte)'3';
        else
            bytes[^1] = 255;
        var parsed = Incits378Codec.Parse(bytes);
        parsed.Should().Fail().Which.Diagnostics.Should().NotBeEmpty();
    }
}
