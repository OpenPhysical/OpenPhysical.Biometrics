// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

using CSharpFunctionalExtensions;

// Checks that every record field fits its INCITS 378-2004 encoding before serialization.
internal static class Incits378Validator
{
    internal static UnitResult<FormatError> Validate(FingerprintRecord record) =>
        record.Views.IsDefault || record.Views.Length > 255
            ? Invalid(
                "FingerprintViews",
                "A record must contain an initialized collection of at most 255 views."
            )
            : record
                .Views.Select(ValidateView)
                .FirstOrDefault(result => result.IsFailure, UnitResult.Success<FormatError>());

    private static UnitResult<FormatError> ValidateView(FingerprintView view)
    {
        if (
            view.ViewNumber > 15
            || view.ImpressionType > 15
            || view.Minutiae.IsDefault
            || view.Minutiae.Length > 255
            || view.ExtendedBlocks.IsDefault
        )
            return Invalid("FingerView", "A finger view has invalid or uninitialized fields.");
        if (view.Minutiae.Any(point => point.X > 0x3FFF || point.Y > 0x3FFF || point.Type > 2))
            return Invalid("Minutia", "A minutia exceeds the encoding limits.");
        if (view.ExtendedBlocks.Any(block => block.Data.IsDefault || block.Data.Length > 0xFFFB))
            return Invalid("ExtendedBlock", "An extended block exceeds the encoding limits.");
        return ExtendedLength(view) > ushort.MaxValue
            ? Invalid("ExtendedLength", "The extended area exceeds its length field.")
            : UnitResult.Success<FormatError>();
    }

    internal static long ExtendedLength(FingerprintView view) =>
        view.ExtendedBlocks.Sum(block => 4L + block.Data.Length);

    private static UnitResult<FormatError> Invalid(string code, string message) =>
        UnitResult.Failure(FormatError.Invalid(code, message));
}
