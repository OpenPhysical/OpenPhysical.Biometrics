using System.Collections.Immutable;
using NUnit.Framework;
using FluentAssertions;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Infrastructure.Cbeff;
using OpenPhysical.Domains.Cbeff;

namespace OpenPhysical.Biometrics.Infrastructure.Tests.Cbeff;

[TestFixture]
public class CbeffSerializerTests
{
    private ICbeffSerializer _serializer = null!;
    private readonly string _testDataBasePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData");

    [SetUp]
    public void SetUp()
    {
        _serializer = new CbeffSerializer();
    }

    [Test]
    public void Deserialize_WithEmptyData_ShouldReturnFailure()
    {
        // Arrange
        var emptyData = ImmutableArray<byte>.Empty;

        // Act
        var result = _serializer.Deserialize(emptyData);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("CBEFF data too short");
    }

    [Test]
    public void Deserialize_WithValidPivCbeffData_ShouldReturnSuccess()
    {
        // Arrange
        var testFile = Path.Combine(_testDataBasePath, "GoldenPiv", "01_Golden_PIV", "5FC1A4.dat");
        var cbeffData = File.ReadAllBytes(testFile).ToImmutableArray();

        // Act
        var result = _serializer.Deserialize(cbeffData);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Header.Should().NotBeNull();
        result.Value.BiometricDataBlock.Should().NotBeEmpty();
    }

    [TestCase("V-a001.02_07.fmrpiv")]
    [TestCase("V-a002.02_07.fmrpiv")]
    [TestCase("V-a010.02_07.fmrpiv")]
    [TestCase("V-b001.02_07.fmrpiv")]
    [TestCase("V-b010.02_07.fmrpiv")]
    public void Deserialize_WithSdsFmrPivData_ShouldReturnSuccess(string fileName)
    {
        // Arrange
        var testFile = Path.Combine(_testDataBasePath, "SdsFmrPiv", fileName);
        var cbeffData = File.ReadAllBytes(testFile).ToImmutableArray();

        // Act - Use DeserializeAuto for SDS FMR PIV data (raw CBEFF format)
        var result = _serializer.DeserializeAuto(cbeffData);

        // Assert
        if (result.IsFailure)
        {
            TestContext.WriteLine($"Deserialization failed for {fileName}: {result.Error}");
        }
        result.IsSuccess.Should().BeTrue();
        result.Value.Header.Should().NotBeNull();
        result.Value.BiometricDataBlock.Should().NotBeEmpty();
    }

    [Test]
    public void Deserialize_WithPivCbeffHeader_ShouldParseCorrectly()
    {
        // Arrange
        var testFile = Path.Combine(_testDataBasePath, "GoldenPiv", "01_Golden_PIV", "5FC1A4.dat");
        var cbeffData = File.ReadAllBytes(testFile).ToImmutableArray();

        // Act
        var result = _serializer.Deserialize(cbeffData);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var header = result.Value.Header;

        // CBEFF Header should have valid values per NIST SP 800-76-2
        header.PatronHeaderVersion.Should().Be(0x03);
        header.BdbFormatOwner.Should().Be(0x001B);
        header.BdbFormatType.Should().Be(0x0501); // Table 15: Facial images use 0x0501
        header.BiometricType.Should().Be(0x02); // Facial features
        header.CreationDate.Should().NotBe(default(DateTime));
        header.Creator.Should().HaveCount(18);
        header.FascN.Should().HaveCount(25);
        header.Reserved.Should().HaveCount(4);
    }

    [Test]
    public void Deserialize_WithPivFacialCbeff_ShouldExtractValidData()
    {
        // Arrange
        var testFile = Path.Combine(_testDataBasePath, "GoldenPiv", "01_Golden_PIV", "5FC1A4.dat");
        var cbeffData = File.ReadAllBytes(testFile).ToImmutableArray();

        // Act
        var result = _serializer.Deserialize(cbeffData);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var payload = result.Value;

        // Should have facial image data
        payload.BiometricDataBlock.Should().NotBeEmpty();
        
        // Biometric data block contains facial record header followed by image data
        // The actual image format (JPEG/JPEG2000) is determined by the facial record parsing
    }

    [Test]
    public void Serialize_WithValidPayload_ShouldReturnSuccess()
    {
        // Arrange
        var creator = new byte[18]; // Initialize with zeros
        var fascN = new byte[25];   // Initialize with zeros
        var reserved = new byte[4]; // Initialize with zeros
        
        var biometricData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        
        var headerResult = CbeffHeader.Create(
            patronHeaderVersion: 0x03,
            sbhSecurityOptions: 0x00,
            bdbLength: (uint)biometricData.Length,
            sbLength: 0,
            bdbFormatOwner: 0x001B,
            bdbFormatType: 0x0501,
            creationDate: new DateTime(2024, 6, 25, 16, 35, 45, DateTimeKind.Utc),
            validityNotBefore: new DateTime(2024, 6, 25, 16, 35, 45, DateTimeKind.Utc),
            validityNotAfter: new DateTime(2025, 6, 25, 16, 35, 45, DateTimeKind.Utc),
            biometricType: 0x02,
            biometricSubtype: 0x00,
            quality: 80,
            creator: creator,
            fascN: fascN,
            reserved: reserved
        );

        headerResult.IsSuccess.Should().BeTrue();
        
        var payloadResult = CbeffPayload.Create(headerResult.Value, biometricData);
        payloadResult.IsSuccess.Should().BeTrue();

        // Act
        var result = _serializer.Serialize(payloadResult.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Test]
    public void SerializeWithWrapper_WithPivTag_ShouldAddBcWrapper()
    {
        // Arrange
        var creator = new byte[18];
        var fascN = new byte[25];
        var reserved = new byte[4];
        var biometricData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        
        var headerResult = CbeffHeader.Create(
            patronHeaderVersion: 0x03,
            sbhSecurityOptions: 0x00,
            bdbLength: (uint)biometricData.Length,
            sbLength: 0,
            bdbFormatOwner: 0x001B,
            bdbFormatType: 0x0501,
            creationDate: new DateTime(2024, 6, 25, 16, 35, 45, DateTimeKind.Utc),
            validityNotBefore: new DateTime(2024, 6, 25, 16, 35, 45, DateTimeKind.Utc),
            validityNotAfter: new DateTime(2025, 6, 25, 16, 35, 45, DateTimeKind.Utc),
            biometricType: 0x02,
            biometricSubtype: 0x00,
            quality: 80,
            creator: creator,
            fascN: fascN,
            reserved: reserved
        );

        var payloadResult = CbeffPayload.Create(headerResult.Value, biometricData);
        payloadResult.IsSuccess.Should().BeTrue();

        // Act
        var result = _serializer.SerializeWithWrapper(payloadResult.Value, 0xBC);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        
        // Should start with BC tag
        result.Value[0].Should().Be(0xBC);
    }

    [Test]
    public void RoundTrip_WithPivCbeffData_ShouldPreserveData()
    {
        // Arrange
        var testFile = Path.Combine(_testDataBasePath, "GoldenPiv", "01_Golden_PIV", "5FC1A4.dat");
        var originalData = File.ReadAllBytes(testFile).ToImmutableArray();

        // Act - Deserialize then serialize
        var deserializeResult = _serializer.Deserialize(originalData);
        deserializeResult.IsSuccess.Should().BeTrue();

        var serializeResult = _serializer.SerializeWithWrapper(deserializeResult.Value, 0xBC);
        serializeResult.IsSuccess.Should().BeTrue();

        // Assert - Data should be functionally equivalent
        var newData = serializeResult.Value;
        
        // Re-deserialize to verify structure is preserved
        var reDeserializeResult = _serializer.Deserialize(newData);
        reDeserializeResult.IsSuccess.Should().BeTrue();

        var original = deserializeResult.Value;
        var roundTrip = reDeserializeResult.Value;

        // Headers should match key fields
        original.Header.PatronHeaderVersion.Should().Be(roundTrip.Header.PatronHeaderVersion);
        original.Header.BdbFormatOwner.Should().Be(roundTrip.Header.BdbFormatOwner);
        original.Header.BdbFormatType.Should().Be(roundTrip.Header.BdbFormatType);
        original.Header.BiometricType.Should().Be(roundTrip.Header.BiometricType);
        original.Header.CreationDate.Should().Be(roundTrip.Header.CreationDate);
        original.Header.Creator.Should().BeEquivalentTo(roundTrip.Header.Creator);

        // Biometric data should match
        original.BiometricDataBlock.Should().BeEquivalentTo(roundTrip.BiometricDataBlock);
    }

    [TestCase("V-a001.02_07.fmrpiv")]
    [TestCase("V-a002.02_07.fmrpiv")]
    [TestCase("V-a010.02_07.fmrpiv")]
    [TestCase("V-b001.02_07.fmrpiv")]
    [TestCase("V-b010.02_07.fmrpiv")]
    public void RoundTrip_WithSdsFmrPivData_ShouldPreserveStructure(string fileName)
    {
        // Arrange
        var testFile = Path.Combine(_testDataBasePath, "SdsFmrPiv", fileName);
        var originalData = File.ReadAllBytes(testFile).ToImmutableArray();

        // Act - Deserialize then serialize (SDS FMR PIV is raw CBEFF format)
        var deserializeResult = _serializer.DeserializeAuto(originalData);
        deserializeResult.IsSuccess.Should().BeTrue();

        var serializeResult = _serializer.Serialize(deserializeResult.Value);
        serializeResult.IsSuccess.Should().BeTrue();

        // Re-deserialize to verify structure
        var reDeserializeResult = _serializer.DeserializeAuto(serializeResult.Value);
        reDeserializeResult.IsSuccess.Should().BeTrue();

        // Assert - Structure should be preserved
        var original = deserializeResult.Value;
        var roundTrip = reDeserializeResult.Value;

        original.Header.PatronHeaderVersion.Should().Be(roundTrip.Header.PatronHeaderVersion);
        original.Header.BiometricType.Should().Be(roundTrip.Header.BiometricType);
        original.Header.CreationDate.Should().Be(roundTrip.Header.CreationDate);
        original.BiometricDataBlock.Length.Should().Be(roundTrip.BiometricDataBlock.Length);
    }

    [Test]
    public void Deserialize_WithInvalidHeader_ShouldReturnFailure()
    {
        // Arrange - Create data too small for valid CBEFF header
        var invalidData = ImmutableArray.Create<byte>(0x01, 0x02, 0x03);

        // Act
        var result = _serializer.Deserialize(invalidData);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("CBEFF data too short");
    }

    [Test]
    public void Deserialize_WithMalformedBcTag_ShouldReturnFailure()
    {
        // Arrange - BC tag with invalid length encoding
        var malformedData = ImmutableArray.Create<byte>(0xBC, 0xFF); // Invalid length

        // Act
        var result = _serializer.Deserialize(malformedData);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Test]
    public void SerializeWithWrapper_WithInvalidTag_ShouldReturnFailure()
    {
        // Arrange
        var creator = new byte[18];
        var fascN = new byte[25];
        var reserved = new byte[4];
        var biometricData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        
        var headerResult = CbeffHeader.Create(
            patronHeaderVersion: 0x03,
            sbhSecurityOptions: 0x00,
            bdbLength: (uint)biometricData.Length,
            sbLength: 0,
            bdbFormatOwner: 0x001B,
            bdbFormatType: 0x0501,
            creationDate: new DateTime(2024, 6, 25, 16, 35, 45, DateTimeKind.Utc),
            validityNotBefore: new DateTime(2024, 6, 25, 16, 35, 45, DateTimeKind.Utc),
            validityNotAfter: new DateTime(2025, 6, 25, 16, 35, 45, DateTimeKind.Utc),
            biometricType: 0x02,
            biometricSubtype: 0x00,
            quality: 80,
            creator: creator,
            fascN: fascN,
            reserved: reserved
        );

        var payloadResult = CbeffPayload.Create(headerResult.Value, biometricData);

        // Act - Use tag with primitive bit set (not valid for wrapper)
        var result = _serializer.SerializeWithWrapper(payloadResult.Value, 0x01);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid wrapper tag");
    }
}