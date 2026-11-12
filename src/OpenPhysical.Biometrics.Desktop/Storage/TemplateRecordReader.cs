// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Fingerprints;

internal static class TemplateRecordReader
{
    internal static Result<FingerprintRecord, DesktopError> Parse(byte[] bytes)
    {
        var parsed = bytes.AsSpan().StartsWith("FMR\0"u8) ? ParseFmr(bytes) : ParseCbeff(bytes);
        return parsed.Ensure(
            record => !record.Views.IsDefaultOrEmpty,
            new DesktopError(
                "Storage.Record",
                "The template must contain at least one finger view."
            )
        );
    }

    private static Result<FingerprintRecord, DesktopError> ParseFmr(ReadOnlySpan<byte> bytes) =>
        Incits378Codec
            .Parse(bytes)
            .MapError(error => new DesktopError("Storage.Record", error.Message))
            .Map(parsed => parsed.Record);

    private static Result<FingerprintRecord, DesktopError> ParseCbeff(byte[] bytes)
    {
        if (bytes.Length < CbeffHeader.Size || bytes[0] != 3)
            return new DesktopError(
                "Storage.Record",
                "Select an ANSI/INCITS 378-2004 FMR or unwrapped PIV CBEFF fingerprint file."
            );
        return CbeffCodec
            .Parse(bytes)
            .MapError(error => new DesktopError("Storage.Record", error.Message))
            .Ensure(
                parsed =>
                    parsed.Record.Header.FormatOwner == 0x001B
                    && parsed.Record.Header.FormatType is 0x0201 or 0x0202
                    && parsed.Record.Header.BiometricType == 8,
                new DesktopError(
                    "Storage.Record",
                    "The CBEFF does not declare an INCITS 378 fingerprint block."
                )
            )
            .Bind(parsed => ParseFmr(parsed.Record.BiometricData.AsSpan()));
    }
}
