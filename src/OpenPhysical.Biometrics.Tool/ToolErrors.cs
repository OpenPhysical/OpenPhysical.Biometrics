// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Tool;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Facial;

internal static class ToolErrors
{
    private const string ConversionMessage =
        "Fingerprint conversion failed. Check the raw FMR record, selected finger/view, resolution, and output availability.";

    internal static ToolError Usage(string message) => new(ToolErrorCodes.Usage, message);

    internal static ToolError InspectionReadFailed() =>
        new(
            ToolErrorCodes.InspectionReadFailed,
            "Reading or interpreting the input record failed."
        );

    internal static ToolError InspectionTooLarge() =>
        new(ToolErrorCodes.InspectionTooLarge, "The record exceeds the inspection input limit.");

    internal static ToolError ConversionFailed() =>
        new(ToolErrorCodes.ConversionFailed, ConversionMessage);

    internal static ToolError InvalidFingerprintRecord(IEnumerable<string> lines) =>
        new(ToolErrorCodes.InvalidFingerprintRecord, string.Join(Environment.NewLine, lines));

    internal static ToolError Face(string code) =>
        new(
            code,
            code == ToolErrorCodes.NativeRuntimeUnavailable
                ? "NativeRuntimeUnavailable: install the ONNX Runtime and Skia native assets for this operating system and architecture."
                : $"{code}: check the input, reviewed geometry, model runtime, and output paths."
        );

    internal static ToolError FaceInput() => Face(ToolErrorCodes.InvalidPortraitInput);

    internal static ToolError FaceFileAccess() => Face(ToolErrorCodes.PortraitFileAccessFailed);

    internal static ToolError FaceMetadata() => Face(ToolErrorCodes.InvalidReviewMetadata);

    internal static ToolError FacePreparation() => Face(ToolErrorCodes.PortraitPreparationFailed);

    // Input problems keep their historic code; runtime and model faults map to their own.
    internal static ToolError FromFacial(FacialError error) =>
        error.Code switch
        {
            FacialErrorCodes.NativeRuntimeUnavailable => Face(
                ToolErrorCodes.NativeRuntimeUnavailable
            ),
            FacialErrorCodes.ModelExecutionFailed => Face(ToolErrorCodes.ModelExecutionFailed),
            _ when InputCodes.Contains(error.Code) => FaceInput(),
            _ => FacePreparation(),
        };

    private static readonly ImmutableHashSet<string> InputCodes =
    [
        FacialErrorCodes.InvalidDimensions,
        FacialErrorCodes.InvalidPixelBuffer,
        FacialErrorCodes.EncodedImageSize,
        FacialErrorCodes.UnsupportedImage,
        FacialErrorCodes.ImageDimensionsExceeded,
        FacialErrorCodes.ImageDecodeFailed,
        FacialErrorCodes.ImageNotOpaque,
        FacialErrorCodes.GeometryMismatch,
        FacialErrorCodes.GeometryPointInvalid,
        FacialErrorCodes.FaceIndexOutOfRange,
    ];
}
