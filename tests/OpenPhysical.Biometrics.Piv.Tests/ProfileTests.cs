// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Piv.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Formats.Fingerprints;

[TestFixture]
internal sealed class ProfileTests
{
    private static FingerprintRecord Fingerprints() =>
        new(1, 1, 0x8001, 512, 512, 197, 197, [View(2), View(7)]);

    private static FingerprintView View(byte finger) =>
        new(finger, 0, 0, 80, [new FingerprintMinutia(1, 10, 20, 30, 90)], []);

    private static FaceRecord Face() =>
        new([
            new FaceImage(new(0, 0, 0, 0, 1, 0, 0), [], new(1, 1, 640, 480, 1, 2, 0, 0), [1, 2, 3]),
        ]);

    [Test]
    public void FingerprintHeaderChecksRegisteredEquipmentAndExplicitResolution()
    {
        var valid = Fingerprints();
        PivBiometricProfile.Validate(valid).Should().BeEmpty();
        (FingerprintRecord Record, string Code)[] cases =
        [
            (valid with { ProductOwner = 0 }, "ProductIdentifier"),
            (valid with { ProductType = 0 }, "ProductIdentifier"),
            (valid with { CaptureEquipment = 0x9001 }, "CaptureEquipment"),
            (valid with { CaptureEquipment = 0x8000 }, "CaptureEquipment"),
            (valid with { Width = 0 }, "ImageDimensions"),
            (valid with { Height = 0 }, "ImageDimensions"),
            (valid with { XResolution = 500 }, "Resolution"),
            (valid with { YResolution = 196 }, "Resolution"),
            (valid with { Views = [View(2)] }, "TwoFingerViews"),
            (valid with { Views = [View(2), View(2)] }, "DistinctFingers"),
        ];
        foreach (var item in cases)
            PivBiometricProfile
                .Validate(item.Record)
                .Select(finding => finding.Code)
                .Should()
                .Contain(item.Code);
    }

    [Test]
    public void FingerViewsCheckAvailabilityAndLimitsSeparatelyFromImageGeometry()
    {
        var valid = View(2);
        (FingerprintView View, string Code)[] cases =
        [
            (valid with { FingerPosition = 0 }, "FingerPosition"),
            (valid with { ViewNumber = 1 }, "ViewNumber"),
            (valid with { ImpressionType = 1 }, "PlainImpression"),
            (valid with { Quality = 0 }, "Nfiq1Quality"),
            (valid with { Quality = 254 }, "UnavailableFingerMinutiae"),
            (valid with { Minutiae = [valid.Minutiae[0], valid.Minutiae[0]] }, "DuplicateMinutiae"),
            (
                valid with
                {
                    Minutiae = Enumerable
                        .Range(0, 129)
                        .Select(x => new FingerprintMinutia(1, (ushort)x, 20, 30, 90))
                        .ToImmutableArray(),
                },
                "MinutiaCount"
            ),
            (
                valid with
                {
                    ExtendedBlocks = [new FingerprintExtendedBlock(0x0100, [1])],
                },
                "ExtendedData"
            ),
        ];
        foreach (var item in cases)
            PivBiometricProfile
                .Validate(item.View)
                .Select(finding => finding.Code)
                .Should()
                .Contain(item.Code);
        PivBiometricProfile
            .Validate(valid with { Quality = 255, Minutiae = [] })
            .Should()
            .BeEmpty();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void PivFacialMetadataRequiresFrontalPoseEncoding(bool angle)
    {
        var valid = Face();
        PivBiometricProfile.Validate(valid).Should().BeEmpty();
        var image = valid.Images[0];
        var altered = image with
        {
            Information = angle
                ? image.Information with
                {
                    PoseAngle = 1,
                }
                : image.Information with
                {
                    PoseUncertainty = 1,
                },
        };
        PivBiometricProfile
            .Validate(new FaceRecord([altered]))
            .Select(finding => finding.Code)
            .Should()
            .Contain(angle ? "FrontalPose" : "PoseUncertainty");
    }

    [Test]
    public void FacialMetadataChecksAcquisitionFieldsAndRequiredImageContent()
    {
        var image = Face().Images[0];
        (FaceImage Image, string Code)[] cases =
        [
            (
                image with
                {
                    ImageInformation = image.ImageInformation with { FaceImageType = 0 },
                },
                "FullFrontalImage"
            ),
            (
                image with
                {
                    ImageInformation = image.ImageInformation with { ImageDataType = 2 },
                },
                "ImageEncoding"
            ),
            (
                image with
                {
                    ImageInformation = image.ImageInformation with { ColorSpace = 3 },
                },
                "SrgbColorSpace"
            ),
            (
                image with
                {
                    ImageInformation = image.ImageInformation with { SourceType = 0 },
                },
                "CaptureSource"
            ),
            (
                image with
                {
                    ImageInformation = image.ImageInformation with { Width = 420 },
                },
                "ImageDimensions"
            ),
            (
                image with
                {
                    ImageInformation = image.ImageInformation with { Quality = 101 },
                },
                "ImageQuality"
            ),
            (
                image with
                {
                    Information = image.Information with { Expression = 0 },
                },
                "NeutralExpression"
            ),
            (image with { ImageData = [] }, "ImageDataRequired"),
        ];
        foreach (var item in cases)
            PivBiometricProfile
                .Validate(new FaceRecord([item.Image]))
                .Select(finding => finding.Code)
                .Should()
                .Contain(item.Code);
    }

    private static CbeffRecord Wrapped(
        FingerprintRecord record,
        sbyte quality = 80,
        bool earlyValidity = false,
        byte[]? creator = null
    )
    {
        var data = Incits378Codec.Write(record);
        var acquired = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var header = CbeffHeader.Create(
            (uint)data.Length,
            1,
            0x001B,
            0x0201,
            acquired,
            earlyValidity ? acquired.AddDays(-1) : acquired,
            acquired.AddYears(1),
            8,
            0x80,
            quality,
            creator ?? new byte[18],
            new byte[25],
            securityOptions: 0x0D
        );
        return new CbeffRecord(header, ImmutableArray.Create(data), [1]);
    }

    [Test]
    public void ProfileRejectsTheUnsupportedSignedSecurityOptionCombination()
    {
        var record = Wrapped(Fingerprints());
        var bytes = record.Header.ToArray();
        bytes[1] = 0x0F;
        record = record with { Header = new CbeffHeader(bytes) };
        var findings = PivBiometricProfile.Validate(
            record,
            new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero)
        );
        findings.Select(finding => finding.Code).Should().Contain("SignedPivSecurityOptions");
    }

    [Test]
    public void ValidityStartsAtOrAfterAcquisition()
    {
        var at = new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);
        PivBiometricProfile.Validate(Wrapped(Fingerprints()), at).Should().BeEmpty();
        PivBiometricProfile
            .Validate(Wrapped(Fingerprints(), earlyValidity: true), at)
            .Select(finding => finding.Code)
            .Should()
            .Contain("ValidityStartsBeforeAcquisition");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void CreatorHasAPrintableAsciiNameAndNullTerminator(bool missingTerminator)
    {
        var creator = Enumerable.Repeat((byte)'A', 18).ToArray();
        if (!missingTerminator)
        {
            creator[0] = 1;
            creator[17] = 0;
        }
        var at = new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);
        PivBiometricProfile
            .Validate(Wrapped(Fingerprints(), creator: creator), at)
            .Select(finding => finding.Code)
            .Should()
            .Contain("CreatorEncoding");
    }

    [Test]
    public void AvailableFingerprintHeaderQualityUsesTheOriginalNfiqScale()
    {
        var at = new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);
        PivBiometricProfile
            .Validate(Wrapped(Fingerprints(), quality: 13), at)
            .Select(finding => finding.Code)
            .Should()
            .Contain("FingerprintHeaderQuality");
    }

    [Test]
    public void AvailableFingerPrecedesAnUnavailableFinger()
    {
        var record = Fingerprints() with
        {
            Views = [View(2) with { Quality = 255, Minutiae = [] }, View(7)],
        };
        PivBiometricProfile
            .Validate(record)
            .Select(finding => finding.Code)
            .Should()
            .Contain("AvailableFingerOrder");
    }
}
