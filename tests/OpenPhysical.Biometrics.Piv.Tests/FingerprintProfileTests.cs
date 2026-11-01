// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;

[TestFixture]
internal sealed class FingerprintProfileTests
{
    [Test]
    public void FingerprintHeaderChecksRegisteredEquipmentAndExplicitResolution()
    {
        var valid = ProfileTestData.Fingerprints();
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
            (valid with { Views = [ProfileTestData.View(2)] }, "TwoFingerViews"),
            (
                valid with
                {
                    Views = [ProfileTestData.View(2), ProfileTestData.View(2)],
                },
                "DistinctFingers"
            ),
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
        var valid = ProfileTestData.View(2);
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

    [Test]
    public void AvailableFingerPrecedesAnUnavailableFinger()
    {
        var record = ProfileTestData.Fingerprints() with
        {
            Views =
            [
                ProfileTestData.View(2) with
                {
                    Quality = 255,
                    Minutiae = [],
                },
                ProfileTestData.View(7),
            ],
        };
        PivBiometricProfile
            .Validate(record)
            .Select(finding => finding.Code)
            .Should()
            .Contain("AvailableFingerOrder");
    }

    [Test]
    public void UnencodableRecordsAreReportedAsInvalidRecordsInsteadOfThrowing()
    {
        var record = ProfileTestData.Fingerprints() with
        {
            Views = [ProfileTestData.View(2) with { ViewNumber = 16 }, ProfileTestData.View(7)],
        };
        PivBiometricProfile
            .Validate(record)
            .Select(finding => finding.Code)
            .Should()
            .Contain("InvalidRecord");
    }
}
