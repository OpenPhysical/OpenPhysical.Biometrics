using CSharpFunctionalExtensions;
using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Infrastructure.Utilities;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace OpenPhysical.Biometrics.Infrastructure.Tests.Utilities;

[TestFixture]
public class BerTlvTests
{
    #region ParseTlv Tests - Short Form Length

    [Test]
    public void ParseTlv_WithShortFormLength_SingleByte_ReturnsSuccess()
    {
        var data = new byte[] { 0xBC, 0x05, 0x01, 0x02, 0x03, 0x04, 0x05 };
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Tag.Should().Be(0xBC);
        result.Value.Value.Should().BeEquivalentTo(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });
        result.Value.BytesConsumed.Should().Be(7);
    }

    [Test]
    public void ParseTlv_WithShortFormLength_MaxValue_ReturnsSuccess()
    {
        var value = Enumerable.Range(0, 127).Select(i => (byte)i).ToArray();
        var data = new byte[] { 0xBC, 0x7F }.Concat(value).ToArray();
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Tag.Should().Be(0xBC);
        result.Value.Value.Should().BeEquivalentTo(value);
        result.Value.BytesConsumed.Should().Be(129);
    }

    [Test]
    public void ParseTlv_WithShortFormLength_Zero_ReturnsSuccess()
    {
        var data = new byte[] { 0xBC, 0x00 };
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Tag.Should().Be(0xBC);
        result.Value.Value.Should().BeEmpty();
        result.Value.BytesConsumed.Should().Be(2);
    }

    #endregion

    #region ParseTlv Tests - Long Form Length

    [Test]
    public void ParseTlv_WithLongFormLength_OneByte_ReturnsSuccess()
    {
        var value = Enumerable.Range(0, 200).Select(i => (byte)i).ToArray();
        var data = new byte[] { 0xBC, 0x81, 0xC8 }.Concat(value).ToArray();
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Tag.Should().Be(0xBC);
        result.Value.Value.Should().BeEquivalentTo(value);
        result.Value.BytesConsumed.Should().Be(203);
    }

    [Test]
    public void ParseTlv_WithLongFormLength_TwoBytes_ReturnsSuccess()
    {
        var value = Enumerable.Range(0, 300).Select(i => (byte)i).ToArray();
        var data = new byte[] { 0xBC, 0x82, 0x01, 0x2C }.Concat(value).ToArray();
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Tag.Should().Be(0xBC);
        result.Value.Value.Should().BeEquivalentTo(value);
        result.Value.BytesConsumed.Should().Be(304);
    }

    [Test]
    public void ParseTlv_WithLongFormLength_ThreeBytes_ReturnsSuccess()
    {
        var value = Enumerable.Range(0, 65536).Select(i => (byte)(i % 256)).ToArray();
        var data = new byte[] { 0xBC, 0x83, 0x01, 0x00, 0x00 }.Concat(value).ToArray();
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Tag.Should().Be(0xBC);
        result.Value.Value.Length.Should().Be(65536);
        result.Value.BytesConsumed.Should().Be(65541);
    }

    [Test]
    public void ParseTlv_WithLongFormLength_FourBytes_ReturnsSuccess()
    {
        var valueLength = 16777216;
        var value = new byte[valueLength];
        var data = new byte[] { 0xBC, 0x84, 0x01, 0x00, 0x00, 0x00 }.Concat(value).ToArray();
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Tag.Should().Be(0xBC);
        result.Value.Value.Length.Should().Be(valueLength);
        result.Value.BytesConsumed.Should().Be(valueLength + 6);
    }

    #endregion

    #region ParseTlv Tests - Error Cases

    [Test]
    public void ParseTlv_WithNullData_ReturnsFailure()
    {
        byte[] data = null;
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Test]
    public void ParseTlv_WithEmptyData_ReturnsFailure()
    {
        var data = Array.Empty<byte>();
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid offset");
    }

    [Test]
    public void ParseTlv_WithSingleByte_ReturnsFailure()
    {
        var data = new byte[] { 0xBC };
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Insufficient data");
    }

    [Test]
    public void ParseTlv_WithInvalidOffset_Negative_ReturnsFailure()
    {
        var data = new byte[] { 0xBC, 0x05, 0x01, 0x02, 0x03, 0x04, 0x05 };
        
        var result = BerTlv.ParseTlv(data, -1);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid offset");
    }

    [Test]
    public void ParseTlv_WithInvalidOffset_TooLarge_ReturnsFailure()
    {
        var data = new byte[] { 0xBC, 0x05, 0x01, 0x02, 0x03, 0x04, 0x05 };
        
        var result = BerTlv.ParseTlv(data, 10);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid offset");
    }

    [Test]
    public void ParseTlv_WithInsufficientDataForValue_ReturnsFailure()
    {
        var data = new byte[] { 0xBC, 0x10, 0x01, 0x02 };
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Insufficient data for value");
    }

    [Test]
    public void ParseTlv_WithIndefiniteLength_ReturnsFailure()
    {
        var data = new byte[] { 0xBC, 0x80, 0x01, 0x02, 0x03 };
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Indefinite length encoding not supported");
    }

    [Test]
    public void ParseTlv_WithLongFormLengthTooLong_ReturnsFailure()
    {
        var data = new byte[] { 0xBC, 0x85, 0x01, 0x00, 0x00, 0x00, 0x00 };
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Length encoding too long");
    }

    [Test]
    public void ParseTlv_WithInsufficientDataForLongFormLength_ReturnsFailure()
    {
        var data = new byte[] { 0xBC, 0x82, 0x01 };
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Insufficient data for long form length");
    }

    #endregion

    #region ParseTlv Tests - With Offset

    [Test]
    public void ParseTlv_WithOffset_ParsesCorrectly()
    {
        var data = new byte[] { 0xFF, 0xFF, 0xFF, 0xBC, 0x03, 0x01, 0x02, 0x03 };
        
        var result = BerTlv.ParseTlv(data, 3);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Tag.Should().Be(0xBC);
        result.Value.Value.Should().BeEquivalentTo(new byte[] { 0x01, 0x02, 0x03 });
        result.Value.BytesConsumed.Should().Be(5);
    }

    #endregion

    #region ParseTlv Tests - ImmutableArray Overload

    [Test]
    public void ParseTlv_WithImmutableArray_ParsesCorrectly()
    {
        var data = ImmutableArray.Create<byte>(0xBC, 0x03, 0x01, 0x02, 0x03);
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Tag.Should().Be(0xBC);
        result.Value.Value.Should().BeEquivalentTo(new byte[] { 0x01, 0x02, 0x03 });
    }

    [Test]
    public void ParseTlv_WithDefaultImmutableArray_ReturnsFailure()
    {
        var data = default(ImmutableArray<byte>);
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be default");
    }

    #endregion

    #region WrapWithTlv Tests

    [Test]
    public void WrapWithTlv_WithShortFormLength_CreatesCorrectStructure()
    {
        var value = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        
        var result = BerTlv.WrapWithTlv(0xBC, value);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new byte[] { 0xBC, 0x05, 0x01, 0x02, 0x03, 0x04, 0x05 });
    }

    [Test]
    public void WrapWithTlv_WithLongFormLength_OneByte_CreatesCorrectStructure()
    {
        var value = Enumerable.Range(0, 200).Select(i => (byte)i).ToArray();
        
        var result = BerTlv.WrapWithTlv(0xBC, value);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Take(3).Should().BeEquivalentTo(new byte[] { 0xBC, 0x81, 0xC8 });
        result.Value.Skip(3).Should().BeEquivalentTo(value);
    }

    [Test]
    public void WrapWithTlv_WithLongFormLength_TwoBytes_CreatesCorrectStructure()
    {
        var value = Enumerable.Range(0, 300).Select(i => (byte)i).ToArray();
        
        var result = BerTlv.WrapWithTlv(0xBC, value);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Take(4).Should().BeEquivalentTo(new byte[] { 0xBC, 0x82, 0x01, 0x2C });
        result.Value.Skip(4).Should().BeEquivalentTo(value);
    }

    [Test]
    public void WrapWithTlv_WithEmptyValue_CreatesCorrectStructure()
    {
        var value = Array.Empty<byte>();
        
        var result = BerTlv.WrapWithTlv(0xBC, value);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new byte[] { 0xBC, 0x00 });
    }

    [Test]
    public void WrapWithTlv_WithNullValue_ReturnsFailure()
    {
        byte[] value = null;
        
        var result = BerTlv.WrapWithTlv(0xBC, value);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Test]
    public void WrapWithTlv_WithImmutableArray_CreatesCorrectStructure()
    {
        var value = ImmutableArray.Create<byte>(0x01, 0x02, 0x03);
        
        var result = BerTlv.WrapWithTlv(0xBC, value);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new byte[] { 0xBC, 0x03, 0x01, 0x02, 0x03 });
    }

    #endregion

    #region Round-trip Tests

    [Test]
    public void RoundTrip_ShortFormLength_PreservesData()
    {
        var originalValue = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var tag = (byte)0xBC;
        
        var wrapResult = BerTlv.WrapWithTlv(tag, originalValue);
        wrapResult.IsSuccess.Should().BeTrue();
        
        var parseResult = BerTlv.ParseTlv(wrapResult.Value);
        
        parseResult.IsSuccess.Should().BeTrue();
        parseResult.Value.Tag.Should().Be(tag);
        parseResult.Value.Value.Should().BeEquivalentTo(originalValue);
    }

    [Test]
    public void RoundTrip_LongFormLength_PreservesData()
    {
        var originalValue = Enumerable.Range(0, 500).Select(i => (byte)(i % 256)).ToArray();
        var tag = (byte)0xBC;
        
        var wrapResult = BerTlv.WrapWithTlv(tag, originalValue);
        wrapResult.IsSuccess.Should().BeTrue();
        
        var parseResult = BerTlv.ParseTlv(wrapResult.Value);
        
        parseResult.IsSuccess.Should().BeTrue();
        parseResult.Value.Tag.Should().Be(tag);
        parseResult.Value.Value.Should().BeEquivalentTo(originalValue);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(127)]
    [TestCase(128)]
    [TestCase(255)]
    [TestCase(256)]
    [TestCase(65535)]
    [TestCase(65536)]
    public void RoundTrip_VariousLengths_PreservesData(int length)
    {
        var originalValue = new byte[length];
        new Random(42).NextBytes(originalValue);
        var tag = (byte)0xBC;
        
        var wrapResult = BerTlv.WrapWithTlv(tag, originalValue);
        wrapResult.IsSuccess.Should().BeTrue();
        
        var parseResult = BerTlv.ParseTlv(wrapResult.Value);
        
        parseResult.IsSuccess.Should().BeTrue();
        parseResult.Value.Tag.Should().Be(tag);
        parseResult.Value.Value.Should().BeEquivalentTo(originalValue);
    }

    #endregion

    #region HasBerTag Tests

    [Test]
    public void HasBerTag_WithMatchingTag_ReturnsTrue()
    {
        var data = new byte[] { 0xBC, 0x05, 0x01, 0x02, 0x03, 0x04, 0x05 };
        
        var result = BerTlv.HasBerTag(data, 0xBC);
        
        result.Should().BeTrue();
    }

    [Test]
    public void HasBerTag_WithNonMatchingTag_ReturnsFalse()
    {
        var data = new byte[] { 0xBC, 0x05, 0x01, 0x02, 0x03, 0x04, 0x05 };
        
        var result = BerTlv.HasBerTag(data, 0xBD);
        
        result.Should().BeFalse();
    }

    [Test]
    public void HasBerTag_WithEmptyData_ReturnsFalse()
    {
        var data = Array.Empty<byte>();
        
        var result = BerTlv.HasBerTag(data, 0xBC);
        
        result.Should().BeFalse();
    }

    [Test]
    public void HasBerTag_WithNullData_ReturnsFalse()
    {
        byte[] data = null;
        
        var result = BerTlv.HasBerTag(data, 0xBC);
        
        result.Should().BeFalse();
    }

    [Test]
    public void HasBerTag_WithImmutableArray_WithMatchingTag_ReturnsTrue()
    {
        var data = ImmutableArray.Create<byte>(0xBC, 0x05, 0x01, 0x02, 0x03, 0x04, 0x05);
        
        var result = BerTlv.HasBerTag(data, 0xBC);
        
        result.Should().BeTrue();
    }

    [Test]
    public void HasBerTag_WithDefaultImmutableArray_ReturnsFalse()
    {
        var data = default(ImmutableArray<byte>);
        
        var result = BerTlv.HasBerTag(data, 0xBC);
        
        result.Should().BeFalse();
    }

    #endregion

    #region ExtractValueIfTagged Tests

    [Test]
    public void ExtractValueIfTagged_WithMatchingTag_ExtractsValue()
    {
        var value = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var tlvData = new byte[] { 0xBC, 0x05 }.Concat(value).ToArray();
        
        var result = BerTlv.ExtractValueIfTagged(tlvData, 0xBC);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(value);
    }

    [Test]
    public void ExtractValueIfTagged_WithNonMatchingTag_ReturnsOriginalData()
    {
        var data = new byte[] { 0xBD, 0x05, 0x01, 0x02, 0x03, 0x04, 0x05 };
        
        var result = BerTlv.ExtractValueIfTagged(data, 0xBC);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(data);
    }

    [Test]
    public void ExtractValueIfTagged_WithNullData_ReturnsFailure()
    {
        byte[] data = null;
        
        var result = BerTlv.ExtractValueIfTagged(data, 0xBC);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Test]
    public void ExtractValueIfTagged_WithImmutableArray_ExtractsValue()
    {
        var value = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var tlvData = ImmutableArray.Create<byte>(0xBC, 0x05).AddRange(value);
        
        var result = BerTlv.ExtractValueIfTagged(tlvData, 0xBC);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(value);
    }

    [Test]
    public void ExtractValueIfTagged_WithDefaultImmutableArray_ReturnsFailure()
    {
        var data = default(ImmutableArray<byte>);
        
        var result = BerTlv.ExtractValueIfTagged(data, 0xBC);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be default");
    }

    #endregion

    #region Real PIV CBEFF Pattern Tests

    [Test]
    public void ParseTlv_WithRealPivCbeffPattern_ParsesCorrectly()
    {
        var cbeffHeader = new byte[] 
        { 
            0x53, 0x01, 0x01,
            0x54, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x55, 0x04, 0x00, 0x01, 0x02, 0x03
        };
        var facialData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var value = cbeffHeader.Concat(facialData).ToArray();
        var data = new byte[] { 0xBC, (byte)(value.Length) }.Concat(value).ToArray();
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Tag.Should().Be(0xBC);
        result.Value.Value.Take(3).Should().BeEquivalentTo(new byte[] { 0x53, 0x01, 0x01 });
        result.Value.Value.Skip(cbeffHeader.Length).Take(4).Should().BeEquivalentTo(facialData);
    }

    [Test]
    public void WrapWithTlv_ForPivCbeffData_CreatesValidStructure()
    {
        var cbeffData = new byte[300];
        new Random(42).NextBytes(cbeffData);
        
        var result = BerTlv.WrapWithTlv(0xBC, cbeffData);
        
        result.IsSuccess.Should().BeTrue();
        
        var parseResult = BerTlv.ParseTlv(result.Value);
        parseResult.IsSuccess.Should().BeTrue();
        parseResult.Value.Tag.Should().Be(0xBC);
        parseResult.Value.Value.Should().BeEquivalentTo(cbeffData);
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Test]
    public void ParseTlv_WithMaxByteValues_HandlesCorrectly()
    {
        var value = Enumerable.Repeat((byte)0xFF, 5).ToArray();
        var data = new byte[] { 0xFF, 0x05 }.Concat(value).ToArray();
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Tag.Should().Be(0xFF);
        result.Value.Value.Should().BeEquivalentTo(value);
    }

    [Test]
    public void ParseTlv_WithLongFormLength_BoundaryAt127_ParsesAsShortForm()
    {
        var value = new byte[127];
        var data = new byte[] { 0xBC, 0x7F }.Concat(value).ToArray();
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Length.Should().Be(127);
    }

    [Test]
    public void ParseTlv_WithLongFormLength_BoundaryAt128_ParsesAsLongForm()
    {
        var value = new byte[128];
        var data = new byte[] { 0xBC, 0x81, 0x80 }.Concat(value).ToArray();
        
        var result = BerTlv.ParseTlv(data);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Length.Should().Be(128);
    }

    [Test]
    public void WrapWithTlv_WithMaxSupportedLength_CreatesValidStructure()
    {
        var maxLength = 16777215;
        var value = new byte[maxLength];
        
        var result = BerTlv.WrapWithTlv(0xBC, value);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Take(5).Should().BeEquivalentTo(new byte[] { 0xBC, 0x83, 0xFF, 0xFF, 0xFF });
    }

    #endregion

    #region TlvParseResult Tests

    [Test]
    public void TlvParseResult_Create_WithValidData_ReturnsSuccess()
    {
        var value = new byte[] { 0x01, 0x02, 0x03 };
        
        var result = BerTlv.TlvParseResult.Create(0xBC, value, 5);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Tag.Should().Be(0xBC);
        result.Value.Value.Should().BeEquivalentTo(value);
        result.Value.BytesConsumed.Should().Be(5);
    }

    [Test]
    public void TlvParseResult_Create_WithNullValue_ReturnsFailure()
    {
        var result = BerTlv.TlvParseResult.Create(0xBC, null, 5);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Value cannot be null");
    }

    [Test]
    public void TlvParseResult_Create_WithInvalidBytesConsumed_ReturnsFailure()
    {
        var value = new byte[] { 0x01, 0x02, 0x03 };
        
        var result = BerTlv.TlvParseResult.Create(0xBC, value, 1);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Bytes consumed must be at least 2");
    }

    #endregion
}