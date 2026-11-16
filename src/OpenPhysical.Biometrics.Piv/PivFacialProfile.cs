// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Faces;

// The SP 800-76-2 facial record metadata profile, separate from image-processing assessments.
internal static class PivFacialProfile
{
    internal static ImmutableArray<BiometricProfileDiagnostic> Validate(FaceRecord record)
    {
        if (record.Images.IsDefaultOrEmpty)
            return ProfileFindings.Failed("Images", (true, "FacialImageRequired"));
        var images = record.Images.SelectMany(
            (image, index) => ValidateImage(image, $"Images[{index}]")
        );
        // The codec is authoritative for representability.
        return [.. images, .. ValidateEncoding(record)];
    }

    private static ImmutableArray<BiometricProfileDiagnostic> ValidateImage(
        FaceImage image,
        string path
    )
    {
        return
        [
            .. ValidateInformation(image.ImageInformation, path),
            .. ProfileFindings.Failed(
                path,
                (image.Information.Expression != 1, "NeutralExpression"),
                (image.Information.PoseAngle != 0, "FrontalPose"),
                (image.Information.PoseUncertainty != 0, "PoseUncertainty"),
                (image.ImageData.IsDefaultOrEmpty, "ImageDataRequired")
            ),
        ];
    }

    internal static ImmutableArray<BiometricProfileDiagnostic> ValidateInformation(
        FaceImageInformation info,
        string path
    ) =>
        ProfileFindings.Failed(
            path,
            (info.FaceImageType != 1, "FullFrontalImage"),
            (info.ImageDataType is not (0 or 1), "ImageEncoding"),
            (info.ColorSpace != 1, "SrgbColorSpace"),
            (info.SourceType is not (2 or 6), "CaptureSource"),
            (
                !PivPortraitGeometry.HasSupportedImageDimensions(info.Width, info.Height),
                "ImageDimensions"
            ),
            (info.Quality > 100, "ImageQuality")
        );

    private static ImmutableArray<BiometricProfileDiagnostic> ValidateEncoding(FaceRecord record) =>
        Incits385Codec
            .Write(record)
            .Match(
                _ => ImmutableArray<BiometricProfileDiagnostic>.Empty,
                _ => ProfileFindings.Failed("Record", (true, "InvalidRecord"))
            );
}
