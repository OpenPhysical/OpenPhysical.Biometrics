// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;

// The SP 800-76-2 fingerprint record profile, independent of cryptographic verification.
internal static class PivFingerprintProfile
{
    private const int MaximumRecordLength = 1574;

    internal static ImmutableArray<BiometricProfileDiagnostic> Validate(FingerprintRecord record)
    {
        var header = ValidateHeader(record);
        if (record.Views.IsDefault || record.Views.Length != 2)
            return header.Add(new("TwoFingerViews", "Views"));
        var selection = ProfileFindings.Failed(
            "Views",
            (
                record.Views.Select(view => view.FingerPosition).Distinct().Count() != 2,
                "DistinctFingers"
            ),
            (
                record.Views[0].Quality is 254 or 255 && record.Views[1].Quality <= 100,
                "AvailableFingerOrder"
            )
        );
        var views = record.Views.SelectMany(
            (view, index) =>
                ValidateView(view)
                    .Select(finding => finding with { Field = $"Views[{index}].{finding.Field}" })
        );
        // The codec is authoritative for representability, coordinates and angles.
        return [.. header, .. selection, .. views, .. ValidateEncoding(record)];
    }

    internal static ImmutableArray<BiometricProfileDiagnostic> ValidateHeader(
        FingerprintRecord record
    ) =>
        ProfileFindings
            .Failed(
                "Product",
                (record.ProductOwner == 0 || record.ProductType == 0, "ProductIdentifier")
            )
            .AddRange(
                ProfileFindings.Failed(
                    "CaptureEquipment",
                    (
                        (record.CaptureEquipment & 0xf000) != 0x8000
                            || (record.CaptureEquipment & 0x0fff) == 0,
                        "CaptureEquipment"
                    )
                )
            )
            .AddRange(
                ProfileFindings.Failed(
                    "Resolution",
                    (record.XResolution != 197 || record.YResolution != 197, "Resolution")
                )
            )
            .AddRange(
                ProfileFindings.Failed(
                    "Dimensions",
                    (record.Width == 0 || record.Height == 0, "ImageDimensions")
                )
            );

    internal static ImmutableArray<BiometricProfileDiagnostic> ValidateView(FingerprintView view) =>
        [.. ValidateSelection(view), .. ValidateMinutiae(view)];

    private static ImmutableArray<BiometricProfileDiagnostic> ValidateSelection(
        FingerprintView view
    ) =>
        [
            .. ProfileFindings.Failed(
                "FingerPosition",
                (view.FingerPosition is < 1 or > 10, "FingerPosition")
            ),
            .. ProfileFindings.Failed("ViewNumber", (view.ViewNumber != 0, "ViewNumber")),
            .. ProfileFindings.Failed(
                "ImpressionType",
                (view.ImpressionType is not (0 or 2), "PlainImpression")
            ),
            .. ProfileFindings.Failed(
                "Quality",
                (view.Quality is not (20 or 40 or 60 or 80 or 100 or 254 or 255), "Nfiq1Quality")
            ),
        ];

    private static ImmutableArray<BiometricProfileDiagnostic> ValidateMinutiae(FingerprintView view)
    {
        var minutiae = view.Minutiae;
        var populated = !minutiae.IsDefault;
        return
        [
            .. ProfileFindings.Failed(
                "Minutiae",
                (minutiae.IsDefault || minutiae.Length > 128, "MinutiaCount")
            ),
            .. ProfileFindings.Failed(
                "ExtendedBlocks",
                (view.ExtendedBlocks.IsDefault || !view.ExtendedBlocks.IsEmpty, "ExtendedData")
            ),
            .. ProfileFindings.Failed(
                "Minutiae",
                (
                    populated && view.Quality is 254 or 255 && !minutiae.IsEmpty,
                    "UnavailableFingerMinutiae"
                ),
                (
                    populated
                        && minutiae.Select(point => (point.X, point.Y)).Distinct().Count()
                            != minutiae.Length,
                    "DuplicateMinutiae"
                )
            ),
        ];
    }

    private static ImmutableArray<BiometricProfileDiagnostic> ValidateEncoding(
        FingerprintRecord record
    ) =>
        Incits378Codec
            .Write(record)
            .Match(
                bytes =>
                    ProfileFindings.Failed(
                        "Record",
                        (bytes.Length > MaximumRecordLength, "RecordLength")
                    ),
                _ => ProfileFindings.Failed("Record", (true, "InvalidRecord"))
            );
}
