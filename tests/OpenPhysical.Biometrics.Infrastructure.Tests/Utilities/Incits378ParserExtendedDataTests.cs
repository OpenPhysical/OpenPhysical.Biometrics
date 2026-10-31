namespace OpenPhysical.Biometrics.Infrastructure.Tests.Utilities;

using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using OpenPhysical.Biometrics.Infrastructure.Utilities;
using OpenPhysical.Domains.Cbeff.Formats.Incits378;

[TestFixture]
internal class Incits378ParserExtendedDataTests
{
    [Test]
    public void ParseIncits378Record_WithRidgeCountData_ParsesCorrectly()
    {
        // Arrange - Minimal INCITS 378 record with ridge count extended data
        var data = new List<byte>();

        // General record header (26 bytes)
        data.AddRange(new byte[] { 0x46, 0x4D, 0x52, 0x00 }); // Format identifier "FMR\0"
        data.AddRange(new byte[] { 0x20, 0x32, 0x30, 0x00 }); // Version " 20\0"
        data.AddRange(new byte[] { 0x00, 0x00 }); // Record length placeholder
        data.AddRange(new byte[] { 0x00, 0x00 }); // Product owner
        data.AddRange(new byte[] { 0x00, 0x00 }); // Product type
        data.AddRange(new byte[] { 0x80, 0x01 }); // Appendix F equipment 1
        data.AddRange(new byte[] { 0x01, 0xF4 }); // Image size X (500)
        data.AddRange(new byte[] { 0x01, 0xF4 }); // Image size Y (500)
        data.AddRange(new byte[] { 0x00, 0xC5 }); // X resolution (197)
        data.AddRange(new byte[] { 0x00, 0xC5 }); // Y resolution (197)
        data.Add(0x01); // Number of finger records
        data.Add(0x00); // Reserved

        // Finger view record
        data.Add(0x02); // Finger position (right index)
        data.Add(0x00); // View number (0) and impression type (0)
        data.Add(0x50); // Finger quality (80)
        data.Add(0x03); // Number of minutiae

        // Three minutiae (6 bytes each)
        // Minutia 1: Type=Other(0), X=100, Y=200, Angle=45, Quality=85
        data.AddRange(new byte[] { 0x00, 0x64 }); // Type (0) and X (100)
        data.AddRange(new byte[] { 0x00, 0xC8 }); // Reserved and Y (200)
        data.Add(45); // Angle
        data.Add(85); // Quality

        // Minutia 2: Type=RidgeEnding(1), X=150, Y=250, Angle=90, Quality=90
        data.AddRange(new byte[] { 0x40, 0x96 }); // Type (1) and X (150)
        data.AddRange(new byte[] { 0x00, 0xFA }); // Reserved and Y (250)
        data.Add(90); // Angle
        data.Add(90); // Quality

        // Minutia 3: Type=Other(0), X=200, Y=300, Angle=135, Quality=95
        data.AddRange(new byte[] { 0x00, 0xC8 }); // Type (0) and X (200)
        data.AddRange(new byte[] { 0x01, 0x2C }); // Reserved and Y (300)
        data.Add(135); // Angle
        data.Add(95); // Quality

        // Extended data block with ridge count data
        data.AddRange(new byte[] { 0x00, 0x0E }); // Extended data block length (14 bytes)
        data.AddRange(new byte[] { 0x00, 0x01 }); // Type ID (0x0001 = Ridge Count)
        data.AddRange(new byte[] { 0x00, 0x0E }); // Data length (14 bytes total)
        data.Add(0x00); // Extraction method (Nonspecific)

        // Ridge count entries (3 bytes each)
        data.Add(0x01); // First minutia index
        data.Add(0x02); // Second minutia index
        data.Add(0x05); // Ridge count (5)

        data.Add(0x01); // First minutia index
        data.Add(0x03); // Second minutia index
        data.Add(0x08); // Ridge count (8)

        data.Add(0x02); // First minutia index
        data.Add(0x03); // Second minutia index
        data.Add(0x03); // Ridge count (3)

        // Update record length in header
        var totalLength = (uint)data.Count;
        data[8] = (byte)(totalLength >> 8);
        data[9] = (byte)totalLength;

        // Act
        var result = Incits378Parser.ParseIncits378Record(data.ToImmutableArray());

        // Assert
        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : string.Empty);
        var record = result.Value;
        Assert.That(record.ViewRecords.Length, Is.EqualTo(1));

        var viewRecord = record.ViewRecords[0];
        Assert.That(viewRecord.NumberOfMinutiae, Is.EqualTo(3));
        Assert.That(viewRecord.ExtendedData.Length, Is.EqualTo(1));

        var extendedBlock = viewRecord.ExtendedData[0];
        Assert.That(extendedBlock.TypeIdentificationCode, Is.EqualTo(0x0001));
        Assert.That(extendedBlock, Is.InstanceOf<RidgeCountData>());

        var ridgeCountData = (RidgeCountData)extendedBlock;
        Assert.That(
            ridgeCountData.ExtractionMethod,
            Is.EqualTo(RidgeCountExtractionMethod.Nonspecific)
        );
        Assert.That(ridgeCountData.RidgeCounts.Length, Is.EqualTo(3));

        // Verify ridge count entries
        Assert.That(ridgeCountData.RidgeCounts[0].FirstMinutiaIndex, Is.EqualTo(1));
        Assert.That(ridgeCountData.RidgeCounts[0].SecondMinutiaIndex, Is.EqualTo(2));
        Assert.That(ridgeCountData.RidgeCounts[0].RidgeCount, Is.EqualTo(5));

        Assert.That(ridgeCountData.RidgeCounts[1].FirstMinutiaIndex, Is.EqualTo(1));
        Assert.That(ridgeCountData.RidgeCounts[1].SecondMinutiaIndex, Is.EqualTo(3));
        Assert.That(ridgeCountData.RidgeCounts[1].RidgeCount, Is.EqualTo(8));

        Assert.That(ridgeCountData.RidgeCounts[2].FirstMinutiaIndex, Is.EqualTo(2));
        Assert.That(ridgeCountData.RidgeCounts[2].SecondMinutiaIndex, Is.EqualTo(3));
        Assert.That(ridgeCountData.RidgeCounts[2].RidgeCount, Is.EqualTo(3));
    }

    [Test]
    public void ParseIncits378Record_WithCoreDeltaData_ParsesCorrectly()
    {
        // Arrange - Minimal INCITS 378 record with core/delta extended data
        var data = new List<byte>();

        // General record header (26 bytes)
        data.AddRange(new byte[] { 0x46, 0x4D, 0x52, 0x00 }); // Format identifier "FMR\0"
        data.AddRange(new byte[] { 0x20, 0x32, 0x30, 0x00 }); // Version " 20\0"
        data.AddRange(new byte[] { 0x00, 0x00 }); // Record length placeholder
        data.AddRange(new byte[] { 0x00, 0x00 }); // Product owner
        data.AddRange(new byte[] { 0x00, 0x00 }); // Product type
        data.AddRange(new byte[] { 0x80, 0x01 }); // Appendix F equipment 1
        data.AddRange(new byte[] { 0x01, 0xF4 }); // Image size X (500)
        data.AddRange(new byte[] { 0x01, 0xF4 }); // Image size Y (500)
        data.AddRange(new byte[] { 0x00, 0xC5 }); // X resolution (197)
        data.AddRange(new byte[] { 0x00, 0xC5 }); // Y resolution (197)
        data.Add(0x01); // Number of finger records
        data.Add(0x00); // Reserved

        // Finger view record
        data.Add(0x02); // Finger position (right index)
        data.Add(0x00); // View number (0) and impression type (0)
        data.Add(0x50); // Finger quality (80)
        data.Add(0x01); // Number of minutiae

        // One minutia (6 bytes)
        data.AddRange(new byte[] { 0x00, 0x64 }); // Type (0) and X (100)
        data.AddRange(new byte[] { 0x00, 0xC8 }); // Reserved and Y (200)
        data.Add(45); // Angle
        data.Add(85); // Quality

        // Extended data block with core/delta data
        data.AddRange(new byte[] { 0x00, 0x12 }); // Extended data block length (18 bytes)
        data.AddRange(new byte[] { 0x00, 0x02 }); // Type ID (0x0002 = Core/Delta)
        data.AddRange(new byte[] { 0x00, 0x12 }); // Data length (18 bytes total)

        // Core information
        data.Add(0x41); // Core info: HasAngle (01) + 1 core
        // Core 1: X=300, Y=400, Angle=90
        data.AddRange(new byte[] { 0x01, 0x2C }); // X (300)
        data.AddRange(new byte[] { 0x01, 0x90 }); // Y (400)
        data.Add(90); // Angle

        // Delta information
        data.Add(0x01); // Delta info: HasAngles (00) + 1 delta
        // Delta 1: X=150, Y=250, Angles=30,60,90
        data.AddRange(new byte[] { 0x00, 0x96 }); // X (150)
        data.AddRange(new byte[] { 0x00, 0xFA }); // Y (250)
        data.Add(30); // Angle 1
        data.Add(60); // Angle 2
        data.Add(90); // Angle 3

        // Update record length in header
        var totalLength = (uint)data.Count;
        data[8] = (byte)(totalLength >> 8);
        data[9] = (byte)totalLength;

        // Act
        var result = Incits378Parser.ParseIncits378Record(data.ToImmutableArray());

        // Assert
        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : string.Empty);
        var record = result.Value;
        var viewRecord = record.ViewRecords[0];
        Assert.That(viewRecord.ExtendedData.Length, Is.EqualTo(1));

        var extendedBlock = viewRecord.ExtendedData[0];
        Assert.That(extendedBlock.TypeIdentificationCode, Is.EqualTo(0x0002));
        Assert.That(extendedBlock, Is.InstanceOf<CoreDeltaData>());

        var coreDeltaData = (CoreDeltaData)extendedBlock;
        Assert.That(coreDeltaData.CoreInfoType, Is.EqualTo(CoreInformationType.HasAngle));
        Assert.That(coreDeltaData.Cores.Length, Is.EqualTo(1));
        Assert.That(coreDeltaData.DeltaInfoType, Is.EqualTo(DeltaInformationType.HasAngles));
        Assert.That(coreDeltaData.Deltas.Length, Is.EqualTo(1));

        // Verify core
        var core = coreDeltaData.Cores[0];
        Assert.That(core.XCoordinate, Is.EqualTo(300));
        Assert.That(core.YCoordinate, Is.EqualTo(400));
        Assert.That(core.Angle.HasValue, Is.True);
        Assert.That(core.Angle.Value, Is.EqualTo(90));

        // Verify delta
        var delta = coreDeltaData.Deltas[0];
        Assert.That(delta.XCoordinate, Is.EqualTo(150));
        Assert.That(delta.YCoordinate, Is.EqualTo(250));
        Assert.That(delta.Angles.HasValue, Is.True);
        Assert.That(delta.Angles.Value.Length, Is.EqualTo(3));
        Assert.That(delta.Angles.Value[0], Is.EqualTo(30));
        Assert.That(delta.Angles.Value[1], Is.EqualTo(60));
        Assert.That(delta.Angles.Value[2], Is.EqualTo(90));
    }

    [Test]
    public void ParseIncits378Record_WithNoExtendedData_ParsesCorrectly()
    {
        // Arrange - INCITS 378 record with no extended data (block length = 0x0000)
        var data = new List<byte>();

        // General record header (26 bytes)
        data.AddRange(new byte[] { 0x46, 0x4D, 0x52, 0x00 }); // Format identifier "FMR\0"
        data.AddRange(new byte[] { 0x20, 0x32, 0x30, 0x00 }); // Version " 20\0"
        data.AddRange(new byte[] { 0x00, 0x00 }); // Record length placeholder
        data.AddRange(new byte[] { 0x00, 0x00 }); // Product owner
        data.AddRange(new byte[] { 0x00, 0x00 }); // Product type
        data.AddRange(new byte[] { 0x80, 0x01 }); // Appendix F equipment 1
        data.AddRange(new byte[] { 0x01, 0xF4 }); // Image size X (500)
        data.AddRange(new byte[] { 0x01, 0xF4 }); // Image size Y (500)
        data.AddRange(new byte[] { 0x00, 0xC5 }); // X resolution (197)
        data.AddRange(new byte[] { 0x00, 0xC5 }); // Y resolution (197)
        data.Add(0x01); // Number of finger records
        data.Add(0x00); // Reserved

        // Finger view record
        data.Add(0x02); // Finger position (right index)
        data.Add(0x00); // View number (0) and impression type (0)
        data.Add(0x50); // Finger quality (80)
        data.Add(0x01); // Number of minutiae

        // One minutia (6 bytes)
        data.AddRange(new byte[] { 0x00, 0x64 }); // Type (0) and X (100)
        data.AddRange(new byte[] { 0x00, 0xC8 }); // Reserved and Y (200)
        data.Add(45); // Angle
        data.Add(85); // Quality

        // Extended data block length = 0x0000 (no extended data)
        data.AddRange(new byte[] { 0x00, 0x00 });

        // Update record length in header
        var totalLength = (uint)data.Count;
        data[8] = (byte)(totalLength >> 8);
        data[9] = (byte)totalLength;

        // Act
        var result = Incits378Parser.ParseIncits378Record(data.ToImmutableArray());

        // Assert
        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : string.Empty);
        var record = result.Value;
        var viewRecord = record.ViewRecords[0];
        Assert.That(viewRecord.ExtendedData.Length, Is.EqualTo(0));
        Assert.That(record.HasExtendedData, Is.False);
    }

    [Test]
    public void ParseIncits378Record_WithMultipleExtendedDataBlocks_ParsesAll()
    {
        // Arrange - Record with both ridge count and core/delta data
        var data = new List<byte>();

        // General record header (26 bytes)
        data.AddRange(new byte[] { 0x46, 0x4D, 0x52, 0x00 }); // Format identifier "FMR\0"
        data.AddRange(new byte[] { 0x20, 0x32, 0x30, 0x00 }); // Version " 20\0"
        data.AddRange(new byte[] { 0x00, 0x00 }); // Record length placeholder
        data.AddRange(new byte[] { 0x00, 0x00 }); // Product owner
        data.AddRange(new byte[] { 0x00, 0x00 }); // Product type
        data.AddRange(new byte[] { 0x80, 0x01 }); // Appendix F equipment 1
        data.AddRange(new byte[] { 0x01, 0xF4 }); // Image size X (500)
        data.AddRange(new byte[] { 0x01, 0xF4 }); // Image size Y (500)
        data.AddRange(new byte[] { 0x00, 0xC5 }); // X resolution (197)
        data.AddRange(new byte[] { 0x00, 0xC5 }); // Y resolution (197)
        data.Add(0x01); // Number of finger records
        data.Add(0x00); // Reserved

        // Finger view record
        data.Add(0x02); // Finger position (right index)
        data.Add(0x00); // View number (0) and impression type (0)
        data.Add(0x50); // Finger quality (80)
        data.Add(0x02); // Number of minutiae

        // Two minutiae (6 bytes each)
        data.AddRange(new byte[] { 0x00, 0x64 }); // Type (0) and X (100)
        data.AddRange(new byte[] { 0x00, 0xC8 }); // Reserved and Y (200)
        data.Add(45); // Angle
        data.Add(85); // Quality

        data.AddRange(new byte[] { 0x40, 0x96 }); // Type (1) and X (150)
        data.AddRange(new byte[] { 0x00, 0xFA }); // Reserved and Y (250)
        data.Add(90); // Angle
        data.Add(90); // Quality

        // Extended data block length (8 + 6 = 14 bytes)
        data.AddRange(new byte[] { 0x00, 0x0E });

        // Ridge count data block
        data.AddRange(new byte[] { 0x00, 0x01 }); // Type ID (0x0001)
        data.AddRange(new byte[] { 0x00, 0x08 }); // Data length (8 bytes)
        data.Add(0x00); // Extraction method (Nonspecific)
        data.Add(0x01); // First minutia index
        data.Add(0x02); // Second minutia index
        data.Add(0x05); // Ridge count

        // Core/delta data block (minimal - no cores/deltas)
        data.AddRange(new byte[] { 0x00, 0x02 }); // Type ID (0x0002)
        data.AddRange(new byte[] { 0x00, 0x06 }); // Data length (6 bytes)
        data.Add(0x00); // Core info: NoAdditionalInfo + 0 cores
        data.Add(0x00); // Delta info: NoAdditionalInfo + 0 deltas

        // Update record length in header
        var totalLength = (uint)data.Count;
        data[8] = (byte)(totalLength >> 8);
        data[9] = (byte)totalLength;

        // Act
        var result = Incits378Parser.ParseIncits378Record(data.ToImmutableArray());

        // Assert
        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : string.Empty);
        var record = result.Value;
        var viewRecord = record.ViewRecords[0];
        Assert.That(viewRecord.ExtendedData.Length, Is.EqualTo(2));
        Assert.That(record.HasExtendedData, Is.True);

        // Verify first block is ridge count data
        Assert.That(viewRecord.ExtendedData[0], Is.InstanceOf<RidgeCountData>());
        Assert.That(viewRecord.ExtendedData[0].TypeIdentificationCode, Is.EqualTo(0x0001));

        // Verify second block is core/delta data
        Assert.That(viewRecord.ExtendedData[1], Is.InstanceOf<CoreDeltaData>());
        Assert.That(viewRecord.ExtendedData[1].TypeIdentificationCode, Is.EqualTo(0x0002));
    }

    [Test]
    public void ParseIncits378Record_WithVendorData_ParsesAsVendorBlock()
    {
        // Arrange - Record with vendor-specific extended data
        var data = new List<byte>();

        // General record header (26 bytes)
        data.AddRange(new byte[] { 0x46, 0x4D, 0x52, 0x00 }); // Format identifier "FMR\0"
        data.AddRange(new byte[] { 0x20, 0x32, 0x30, 0x00 }); // Version " 20\0"
        data.AddRange(new byte[] { 0x00, 0x00 }); // Record length placeholder
        data.AddRange(new byte[] { 0x00, 0x00 }); // Product owner
        data.AddRange(new byte[] { 0x00, 0x00 }); // Product type
        data.AddRange(new byte[] { 0x80, 0x01 }); // Appendix F equipment 1
        data.AddRange(new byte[] { 0x01, 0xF4 }); // Image size X (500)
        data.AddRange(new byte[] { 0x01, 0xF4 }); // Image size Y (500)
        data.AddRange(new byte[] { 0x00, 0xC5 }); // X resolution (197)
        data.AddRange(new byte[] { 0x00, 0xC5 }); // Y resolution (197)
        data.Add(0x01); // Number of finger records
        data.Add(0x00); // Reserved

        // Finger view record
        data.Add(0x02); // Finger position
        data.Add(0x00); // View number and impression type
        data.Add(0x50); // Finger quality
        data.Add(0x00); // Number of minutiae (0 - empty template)

        // Extended data block with vendor data
        data.AddRange(new byte[] { 0x00, 0x08 }); // Extended data block length (8 bytes)
        data.AddRange(new byte[] { 0x01, 0x05 }); // Vendor-defined type 0x0105
        data.AddRange(new byte[] { 0x00, 0x08 }); // Data length (8 bytes total)
        data.AddRange(new byte[] { 0x12, 0x34 }); // Vendor code
        data.AddRange(new byte[] { 0xAB, 0xCD }); // Vendor data

        // Update record length in header
        var totalLength = (uint)data.Count;
        data[8] = (byte)(totalLength >> 8);
        data[9] = (byte)totalLength;

        // Act
        var result = Incits378Parser.ParseIncits378Record(data.ToImmutableArray());

        // Assert
        Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error : string.Empty);
        var record = result.Value;
        var viewRecord = record.ViewRecords[0];
        Assert.That(viewRecord.ExtendedData.Length, Is.EqualTo(1));

        var extendedBlock = viewRecord.ExtendedData[0];
        Assert.That(extendedBlock.TypeIdentificationCode, Is.EqualTo(0x0105));
        Assert.That(extendedBlock, Is.InstanceOf<VendorData>());

        var vendorData = (VendorData)extendedBlock;
        Assert.That(vendorData.VendorCode, Is.EqualTo(0x1234));
        Assert.That(vendorData.Data.Length, Is.EqualTo(2));
        Assert.That(vendorData.Data[0], Is.EqualTo(0xAB));
        Assert.That(vendorData.Data[1], Is.EqualTo(0xCD));
    }
}
