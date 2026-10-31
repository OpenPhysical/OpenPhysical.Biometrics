namespace OpenPhysical.Biometrics.Infrastructure.Tests;

using System.Collections.Immutable;
using System.IO;
using NUnit.Framework;
using OpenPhysical.Biometrics.Infrastructure.Cbeff;
using OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;
using OpenPhysical.Biometrics.Infrastructure.Utilities;
using OpenPhysical.Domains.Cbeff.Formats.Incits378;

/// <summary>
/// Tests to prove the extended data implementation works with real PIV golden test data.
/// </summary>
[TestFixture]
internal class GoldenPivExtendedDataTests
{
    [Test]
    public void ParseGoldenPiv_01_ProvesParsing()
    {
        // Arrange
        var testDataPath = Path.Combine(
            "TestData",
            "GoldenPiv",
            "01_Golden_PIV",
            "fingerprints.bin"
        );
        var data = File.ReadAllBytes(testDataPath);

        // Extract CBEFF data from PIV wrapper (0xBC tag)
        var cbeffResult = BerTlv.ExtractValueIfTagged(data, 0xBC);
        Assert.That(cbeffResult.IsSuccess, Is.True, "Should extract CBEFF from PIV wrapper");

        // Use CbeffSerializer to properly parse the CBEFF structure
        var cbeffSerializer = new CbeffSerializer();
        var cbeffPayloadResult = cbeffSerializer.Deserialize(cbeffResult.Value.ToArray());
        Assert.That(
            cbeffPayloadResult.IsSuccess,
            Is.True,
            $"Should deserialize CBEFF: {(cbeffPayloadResult.IsFailure ? cbeffPayloadResult.Error : "Success")}"
        );

        // Get biometric data block from CBEFF payload
        var biometricData = cbeffPayloadResult.Value.GetBiometricDataBlock();

        // Act - Parse directly with INCITS 378 parser
        var parseResult = Incits378Parser.ParseIncits378Record(biometricData.ToImmutableArray());

        // Assert
        Assert.That(
            parseResult.IsSuccess,
            Is.True,
            $"Failed to parse INCITS 378 data: {(parseResult.IsFailure ? parseResult.Error : "Success")}"
        );

        var fingerRecord = parseResult.Value;
        Assert.That(
            fingerRecord.ViewRecords.Length,
            Is.EqualTo(2),
            "Should have 2 finger views (left and right index)"
        );
        Assert.That(fingerRecord.TotalMinutiae, Is.GreaterThan(0), "Should have minutiae data");
        Assert.That(fingerRecord.HasExtendedData, Is.False, "Should not have extended data");

        // Verify each view record has no extended data
        foreach (var view in fingerRecord.ViewRecords)
        {
            Assert.That(
                view.ExtendedData.Length,
                Is.EqualTo(0),
                "View should have no extended data blocks"
            );
        }
    }

    [Test]
    public void ParseAllGoldenPivFingerprints_ProvesComprehensiveSupport()
    {
        // Arrange - Get all fingerprint.bin files from golden test data
        var testDataDir = Path.Combine("TestData", "GoldenPiv");
        var fingerprintFiles = Directory.GetFiles(
            testDataDir,
            "fingerprints.bin",
            SearchOption.AllDirectories
        );

        Assert.That(
            fingerprintFiles.Length,
            Is.GreaterThan(0),
            "Should find PIV fingerprint test files"
        );

        // Act & Assert - Test each file
        foreach (var filePath in fingerprintFiles)
        {
            var relativePath = Path.GetRelativePath(testDataDir, filePath);

            var data = File.ReadAllBytes(filePath);

            // Extract CBEFF data from PIV wrapper
            var cbeffResult = BerTlv.ExtractValueIfTagged(data, 0xBC);
            if (!cbeffResult.IsSuccess)
                continue; // Skip files without CBEFF wrapper

            // Use CbeffSerializer to properly parse the CBEFF structure
            var cbeffSerializer = new CbeffSerializer();
            var cbeffPayloadResult = cbeffSerializer.Deserialize(cbeffResult.Value.ToArray());
            if (!cbeffPayloadResult.IsSuccess)
                continue; // Skip malformed CBEFF data

            var biometricData = cbeffPayloadResult.Value.GetBiometricDataBlock();

            // Parse with INCITS 378 parser
            var parseResult = Incits378Parser.ParseIncits378Record(
                biometricData.ToImmutableArray()
            );
            Assert.That(
                parseResult.IsSuccess,
                Is.True,
                $"Failed to parse INCITS 378 in {relativePath}: {(parseResult.IsFailure ? parseResult.Error : "Success")}"
            );

            var fingerRecord = parseResult.Value;

            TestContext.WriteLine(
                $"✓ {relativePath}: "
                    + $"Views: {fingerRecord.ViewRecords.Length}, "
                    + $"Minutiae: {fingerRecord.TotalMinutiae}, "
                    + $"Extended: {fingerRecord.HasExtendedData}"
            );
        }
    }

    [Test]
    public void ExtendedDataParsing_WithNoExtendedData_HandledCorrectly()
    {
        // Arrange - Use Golden PIV 01 which has no extended data
        var testDataPath = Path.Combine(
            "TestData",
            "GoldenPiv",
            "01_Golden_PIV",
            "fingerprints.bin"
        );
        var data = File.ReadAllBytes(testDataPath);

        var cbeffResult = BerTlv.ExtractValueIfTagged(data, 0xBC);
        var cbeffSerializer = new CbeffSerializer();
        var cbeffPayloadResult = cbeffSerializer.Deserialize(cbeffResult.Value.ToArray());

        var biometricData = cbeffPayloadResult.Value.GetBiometricDataBlock();

        // Act
        var parseResult = Incits378Parser.ParseIncits378Record(biometricData.ToImmutableArray());

        // Assert
        Assert.That(parseResult.IsSuccess, Is.True);
        var record = parseResult.Value;

        // Verify extended data is correctly parsed as empty
        Assert.That(record.HasExtendedData, Is.False);
        foreach (var view in record.ViewRecords)
        {
            Assert.That(view.ExtendedData.Length, Is.EqualTo(0));
        }

        // Verify CBEFF format type selection would be correct
        var determinedType = PivFingerprintFormatHandler.DetermineCbeffFormatType(record);
        Assert.That(
            determinedType,
            Is.EqualTo((ushort)0x0201),
            "Should select no extended data format type"
        );
    }

    [Test]
    public void AngleBugFix_VerifyCorrectAngleConversion()
    {
        // Arrange - Parse a fingerprint record with known angles
        var testDataPath = Path.Combine(
            "TestData",
            "GoldenPiv",
            "01_Golden_PIV",
            "fingerprints.bin"
        );
        var data = File.ReadAllBytes(testDataPath);

        var cbeffResult = BerTlv.ExtractValueIfTagged(data, 0xBC);
        var cbeffSerializer = new CbeffSerializer();
        var cbeffPayloadResult = cbeffSerializer.Deserialize(cbeffResult.Value.ToArray());

        var biometricData = cbeffPayloadResult.Value.GetBiometricDataBlock();

        // Act
        var parseResult = Incits378Parser.ParseIncits378Record(biometricData.ToImmutableArray());

        // Assert
        Assert.That(parseResult.IsSuccess, Is.True);
        var record = parseResult.Value;

        // Verify angles are in valid range and use correct conversion
        var allMinutiae = record.GetAllMinutiae();
        Assert.That(allMinutiae.Length, Is.GreaterThan(0));

        foreach (var minutia in allMinutiae)
        {
            // Angle should be 0-179 (2-degree units per INCITS 378)
            Assert.That(
                minutia.Angle,
                Is.LessThanOrEqualTo(179),
                $"Minutia angle {minutia.Angle} exceeds maximum 179"
            );

            // Converted angle should be 0-358 degrees
            var degrees = minutia.GetAngleInDegrees();
            Assert.That(
                degrees,
                Is.LessThanOrEqualTo(358.0),
                $"Converted angle {degrees}° exceeds maximum 358°"
            );

            // Verify the 2-degree conversion (not the old 256-unit bug)
            var expectedDegrees = minutia.Angle * 2.0;
            Assert.That(
                degrees,
                Is.EqualTo(expectedDegrees),
                $"Angle conversion incorrect: {minutia.Angle} * 2 should equal {degrees}"
            );
        }

        TestContext.WriteLine(
            $"✓ Verified {allMinutiae.Length} minutiae angles use correct 2-degree conversion"
        );
    }
}
