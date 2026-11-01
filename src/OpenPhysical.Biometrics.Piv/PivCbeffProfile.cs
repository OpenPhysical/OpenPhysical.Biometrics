// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Formats.Fingerprints;

// The signed PIV header requirements and the enclosed biometric profile.
internal static class PivCbeffProfile
{
    internal static ImmutableArray<BiometricProfileDiagnostic> Validate(
        CbeffRecord record,
        DateTimeOffset evaluationTime
    ) =>
        [
            .. ValidateHeader(record, evaluationTime),
            .. record.Header.FormatType switch
            {
                0x0201 => ValidateFingerprint(record),
                0x0501 => ValidateFace(record),
                _ => ProfileFindings.Failed("Header", (true, "BiometricFormat")),
            },
        ];

    private static ImmutableArray<BiometricProfileDiagnostic> ValidateHeader(
        CbeffRecord record,
        DateTimeOffset evaluationTime
    )
    {
        var header = record.Header;
        var creator = header.Creator;
        var terminator = creator.AsSpan().IndexOf((byte)0);
        return ProfileFindings.Failed(
            "Header",
            (header.PatronHeaderVersion != 3, "PatronHeaderVersion"),
            (header.SecurityOptions != 0x0D, "SignedPivSecurityOptions"),
            (header.FormatOwner != 0x001B, "FormatOwner"),
            (header.Reserved.Any(value => value != 0), "ReservedBytes"),
            (
                header.ValidityNotBefore > header.ValidityNotAfter
                    || evaluationTime < header.ValidityNotBefore
                    || evaluationTime > header.ValidityNotAfter,
                "ValidityPeriod"
            ),
            (header.CreationDate > evaluationTime, "AcquisitionDate"),
            (header.ValidityNotBefore < header.CreationDate, "ValidityStartsBeforeAcquisition"),
            (
                terminator < 0 || creator.Take(terminator).Any(value => value is < 0x20 or > 0x7E),
                "CreatorEncoding"
            ),
            (header.Quality is < -2 or > 100, "BiometricQuality"),
            (
                record.BiometricData.IsDefault
                    || header.BiometricDataLength != record.BiometricData.Length,
                "BiometricDataLength"
            ),
            (
                record.Signature.IsDefaultOrEmpty
                    || header.SignatureLength != record.Signature.Length,
                "SignatureBlockLength"
            )
        );
    }

    private static ImmutableArray<BiometricProfileDiagnostic> ValidateFingerprint(
        CbeffRecord record
    )
    {
        var header = record.Header;
        var headerFindings = ProfileFindings.Failed(
            "Header",
            (
                header.Quality is not (20 or 40 or 60 or 80 or 100 or -1 or -2),
                "FingerprintHeaderQuality"
            ),
            ((header.BiometricDataType & 0xE0) != 0x80, "ProcessedFingerprintData"),
            (header.BiometricType != 8, "FingerprintModality")
        );
        return
        [
            .. headerFindings,
            .. Incits378Codec
                .Parse(record.BiometricData.AsSpan())
                .Match(
                    parsed => ValidateFingerprintRecord(header, parsed.Record),
                    error => ParseFindings(error)
                ),
        ];
    }

    private static ImmutableArray<BiometricProfileDiagnostic> ValidateFingerprintRecord(
        CbeffHeader header,
        FingerprintRecord fingerprint
    )
    {
        var views = fingerprint.Views;
        var allUnavailable = views.All(view => view.Quality is 254 or 255);
        var unavailableQuality = views.Any(view => view.Quality == 255) ? -1 : -2;
        var inconsistent = allUnavailable
            ? header.Quality != unavailableQuality
            : header.Quality < 0;
        return
        [
            .. PivFingerprintProfile.Validate(fingerprint),
            .. ProfileFindings.Failed(
                "Header",
                (!views.IsDefaultOrEmpty && inconsistent, "FingerprintHeaderQuality")
            ),
        ];
    }

    private static ImmutableArray<BiometricProfileDiagnostic> ValidateFace(CbeffRecord record)
    {
        var header = record.Header;
        var headerFindings = ProfileFindings.Failed(
            "Header",
            (header.Quality == -1, "FacialHeaderQuality"),
            ((header.BiometricDataType & 0xE0) != 0x20, "RawFacialData"),
            (header.BiometricType != 2, "FacialModality")
        );
        return
        [
            .. headerFindings,
            .. Incits385Codec
                .Parse(record.BiometricData.AsSpan())
                .Match(
                    parsed => PivFacialProfile.Validate(parsed.Record),
                    error => ParseFindings(error)
                ),
        ];
    }

    private static ImmutableArray<BiometricProfileDiagnostic> ParseFindings(
        Formats.FormatError error
    ) =>
        error
            .Diagnostics.Select(diagnostic => new BiometricProfileDiagnostic(
                diagnostic.Code,
                "BiometricData"
            ))
            .ToImmutableArray();
}
