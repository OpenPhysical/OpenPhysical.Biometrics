// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Faces;

using CSharpFunctionalExtensions;

// Checks that every facial field fits its INCITS 385-2004 encoding before serialization.
internal static class Incits385Validator
{
    internal static UnitResult<FormatError> Validate(FaceRecord record) =>
        record.Images.IsDefaultOrEmpty || record.Images.Length > ushort.MaxValue
            ? Invalid("ImageCount", "A facial record requires between one and 65535 image blocks.")
            : record
                .Images.Select(ValidateImage)
                .FirstOrDefault(result => result.IsFailure, UnitResult.Success<FormatError>());

    private static UnitResult<FormatError> ValidateImage(FaceImage image) =>
        image.FeaturePoints.IsDefault
        || image.FeaturePoints.Length > ushort.MaxValue
        || image.ImageData.IsDefaultOrEmpty
        || image.Information.FeatureMask > 0xFFFFFF
        || image.Information.PoseAngle > 0xFFFFFF
        || image.Information.PoseUncertainty > 0xFFFFFF
        || BlockLength(image) > uint.MaxValue
            ? Invalid("FaceBlock", "A facial block has missing or oversized fields.")
            : UnitResult.Success<FormatError>();

    internal static long BlockLength(FaceImage image) =>
        32L + 8L * image.FeaturePoints.Length + image.ImageData.Length;

    private static UnitResult<FormatError> Invalid(string code, string message) =>
        UnitResult.Failure(FormatError.Invalid(code, message));
}
