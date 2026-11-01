using System.Buffers.Binary;
using System.Collections.Immutable;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Formats.Fingerprints;

namespace OpenPhysical.Biometrics.Formats.Tests;

/// <summary>Exercises wire layouts and trust boundaries independently of image processing.</summary>
[TestFixture]
public sealed class CanonicalCodecTests
{
    // This hand-authored record has one right-index view and one ridge ending.
    // It checks the byte layout independently of the writer under test.
    private static byte[] FingerprintBytes() =>
        Convert.FromHexString(
            "464D52002032300000260001000280010200020000C500C5010002005001400A00142D640000"
        );

    /// <summary>Checks the independent FMR wire vector and coordinate/angle units.</summary>
    [Test]
    public void FingerprintWireVectorRoundTrips()
    {
        var bytes = FingerprintBytes();
        var parsed = Incits378Codec.Parse(bytes);
        parsed.IsSuccess.Should().BeTrue();
        var record = parsed.Value!;
        using (new AssertionScope())
        {
            record.XResolution.Should().Be(197);
            record.Views.Single().FingerPosition.Should().Be(2);
            record
                .Views.Single()
                .Minutiae.Single()
                .Should()
                .Be(new FingerprintMinutia(1, 10, 20, 45, 100));
            Incits378Codec.Write(record).Should().Equal(bytes);
        }
        Array.Clear(bytes);
        record.Views[0].Minutiae[0].X.Should().Be(10);
    }

    /// <summary>Preserves unknown extended blocks and derives their inclusive lengths.</summary>
    [Test]
    public void UnknownExtendedBlocksPreserveTheirOriginalBytes()
    {
        var original = Incits378Codec.Parse(FingerprintBytes()).Value!;
        var view = original.Views[0] with { ExtendedBlocks = [new(0x4321, [0xAA, 0x55])] };
        var bytes = Incits378Codec.Write(original with { Views = [view] });
        var parsed = Incits378Codec.Parse(bytes);
        parsed.IsSuccess.Should().BeTrue();
        parsed.Value!.Views[0].ExtendedBlocks[0].Data.Should().Equal(new byte[] { 0xAA, 0x55 });
        Incits378Codec.Write(parsed.Value).Should().Equal(bytes);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(bytes.Length - 4), 3);
        Incits378Codec.Parse(bytes).Diagnostics[0].Code.Should().Be("ExtendedBlockLength");
    }

    /// <summary>Preserves the opposite core and delta angle-presence flags.</summary>
    [Test]
    public void CoreAndDeltaAngleFlagsFollowTheirOwnConventions()
    {
        var original = Incits378Codec.Parse(FingerprintBytes()).Value!;
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
        var bytes = Incits378Codec.Write(original with { Views = [view] });
        var parsed = Incits378Codec.Parse(bytes);
        parsed.IsSuccess.Should().BeTrue();
        parsed.Value!.Views[0].ExtendedBlocks[0].Data.Should().Equal(data);
        bytes[^1] = 180;
        Incits378Codec.Parse(bytes).Diagnostics[0].Code.Should().Be("CoreDeltaAngle");
    }

    /// <summary>Exercises the six-byte record-length encoding beyond the short-length boundary.</summary>
    [Test]
    public void ExtendedRecordLengthIsComputedFromAllViews()
    {
        var original = Incits378Codec.Parse(FingerprintBytes()).Value!;
        var block = new FingerprintExtendedBlock(0x4321, ImmutableArray.Create(new byte[35000]));
        var first = original.Views[0] with { ExtendedBlocks = [block] };
        var second = first with { FingerPosition = 7 };
        var bytes = Incits378Codec.Write(original with { Views = [first, second] });
        using (new AssertionScope())
        {
            BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(8)).Should().Be(0);
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(10)).Should().Be((uint)bytes.Length);
            Incits378Codec.Parse(bytes).IsSuccess.Should().BeTrue();
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
        var bytes = FingerprintBytes();
        bytes[offset] = value;
        Incits378Codec.Parse(bytes).IsSuccess.Should().BeFalse();
    }

    /// <summary>Preserves explicit unavailable-finger statuses with empty minutiae collections.</summary>
    /// <param name="quality">The PIV status identifying a temporary or permanent capture unavailability.</param>
    [TestCase(254)]
    [TestCase(255)]
    public void UnavailableViewsRemainEmpty(byte quality)
    {
        var original = Incits378Codec.Parse(FingerprintBytes()).Value!;
        var bytes = Incits378Codec.Write(
            original with
            {
                Views = [original.Views[0] with { Quality = quality, Minutiae = [] }],
            }
        );
        var parsed = Incits378Codec.Parse(bytes).Value!;
        parsed.Views[0].Quality.Should().Be(quality);
        parsed.Views[0].Minutiae.Should().BeEmpty();
    }

    /// <summary>Maintains the full facial quality field and all facial image blocks.</summary>
    [Test]
    public void FaceImagesAndSixteenBitQualityRoundTrip()
    {
        var original = FacialRecord();
        var bytes = Incits385Codec.Write(original);
        var parsed = Incits385Codec.Parse(bytes);
        parsed.IsSuccess.Should().BeTrue();
        using (new AssertionScope())
        {
            parsed.Value!.Images.Length.Should().Be(2);
            parsed.Value.Images[0].ImageInformation.Quality.Should().Be(0xFFFF);
            parsed.Value.Images[1].ImageData.Should().Equal(new byte[] { 1, 2, 3 });
            Incits385Codec.Write(parsed.Value).Should().Equal(bytes);
        }
    }

    /// <summary>Prevents a facial block from borrowing the next image's metadata.</summary>
    [Test]
    public void FacialFeatureCountIsBoundedByItsOwnImageBlock()
    {
        var bytes = Incits385Codec.Write(FacialRecord());
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(18), 10);
        var result = Incits385Codec.Parse(bytes);
        result.IsSuccess.Should().BeFalse();
        result.Diagnostics[0].Code.Should().Be("Truncated");
    }

    /// <summary>Checks exact CBEFF header preservation and detached signed-content boundaries.</summary>
    [Test]
    public void CbeffPreservesExactHeaderAndSignedContent()
    {
        var record = CbeffRecord();
        var bytes = CbeffCodec.Write(record);
        var parsed = CbeffCodec.Parse(bytes);
        parsed.IsSuccess.Should().BeTrue();
        using (new AssertionScope())
        {
            bytes.Length.Should().Be(93);
            parsed.Value!.Header.RawBytes.Should().Equal(record.Header.RawBytes);
            CbeffCodec.GetSignedData(parsed.Value).Should().Equal(bytes[..91]);
            CbeffCodec.Write(parsed.Value).Should().Equal(bytes);
            bytes[12].Should().Be(20);
            bytes[13].Should().Be(26);
        }
        var headerCopy = record.Header.ToArray();
        headerCopy[0] = 0;
        record.Header.PatronHeaderVersion.Should().Be(3);
        record.Header.WithSignatureLength(42).SignatureLength.Should().Be(42);
        record.Header.SignatureLength.Should().Be(2);
    }

    /// <summary>Rejects malformed calendar timestamps and length mismatches.</summary>
    [Test]
    public void CbeffCalendarAndLengthErrorsAreLocated()
    {
        var bytes = CbeffCodec.Write(CbeffRecord());
        bytes[14] = 13;
        var dateResult = CbeffCodec.Parse(bytes);
        dateResult.Diagnostics[0].Code.Should().Be("CbeffTimestamp");
        bytes = CbeffCodec.Write(CbeffRecord());
        bytes[7]++;
        CbeffCodec.Parse(bytes).Diagnostics[0].Offset.Should().Be(2);
    }

    /// <summary>Keeps inspection contents private until an explicit sensitive-value request.</summary>
    [Test]
    public void InspectionValuesRequireExplicitOptIn()
    {
        var bytes = FingerprintBytes();
        var ordinary = Incits378Codec.Parse(bytes);
        var sensitive = Incits378Codec.Parse(bytes, new(IncludeSensitiveValues: true));
        ordinary.Inspection.Fields.All(field => field.Value is null).Should().BeTrue();
        sensitive.Inspection.Fields.Any(field => field.Value == "400A").Should().BeTrue();
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
        var bytes = FingerprintBytes();
        if (version)
            bytes[5] = (byte)'3';
        else
            bytes[^1] = 255;
        var parsed = Incits378Codec.Parse(bytes);
        parsed.IsSuccess.Should().BeFalse();
        parsed.Diagnostics.Should().NotBeEmpty();
    }

    /// <summary>Returns format diagnostics for every truncated prefix.</summary>
    [Test]
    public void EveryTruncatedPrefixReturnsDiagnostics()
    {
        var fingerprint = FingerprintBytes();
        var face = Incits385Codec.Write(FacialRecord());
        var cbeff = CbeffCodec.Write(CbeffRecord());
        for (var i = 0; i < fingerprint.Length; i++)
            Incits378Codec.Parse(fingerprint.AsSpan(0, i)).IsSuccess.Should().BeFalse();
        for (var i = 0; i < face.Length; i++)
            Incits385Codec.Parse(face.AsSpan(0, i)).IsSuccess.Should().BeFalse();
        for (var i = 0; i < cbeff.Length; i++)
            CbeffCodec.Parse(cbeff.AsSpan(0, i)).IsSuccess.Should().BeFalse();
    }

    private static FaceRecord FacialRecord()
    {
        var face = new FaceImage(
            new(0, 0, 0, 0, 1, 0, 0),
            [new(1, 1, 10, 20)],
            new(1, 0, 256, 256, 1, 2, 0, 0xFFFF),
            [1, 2, 3]
        );
        return new([face, face]);
    }

    /// <summary>Rejects oversized unsigned lengths before any buffer slice.</summary>
    /// <param name="declaredLength">A biometric block length exceeding the supplied buffer.</param>
    [TestCase(0x80000000u)]
    [TestCase(uint.MaxValue)]
    public void CbeffRejectsUnsignedLengthsBeyondTheInput(uint declaredLength)
    {
        var bytes = CbeffCodec.Write(CbeffRecord());
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(2, 4), declaredLength);
        var result = CbeffCodec.Parse(bytes);
        result.IsSuccess.Should().BeFalse();
        result.Diagnostics[0].Code.Should().Be("CbeffLength");
        result.Diagnostics[0].Offset.Should().Be(2);
    }

    private static CbeffRecord CbeffRecord()
    {
        var time = new DateTimeOffset(2026, 10, 31, 12, 34, 56, TimeSpan.Zero);
        var header = CbeffHeader.Create(
            3,
            2,
            0x001B,
            0x0201,
            time,
            time,
            time.AddYears(1),
            8,
            0,
            0,
            new byte[18],
            new byte[25]
        );
        return new(header, [1, 2, 3], [4, 5]);
    }
}
