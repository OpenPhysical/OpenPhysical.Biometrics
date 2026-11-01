// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Onnx;

/// <summary>Identifies the assembly containing the pinned MediaPipe model resources.</summary>
public static class MediaPipeModels
{
    /// <summary>The embedded short-range BlazeFace detector resource.</summary>
    public const string Detector =
        "OpenPhysical.Biometrics.Onnx.Resources.face_detection_short_range.onnx";

    /// <summary>The embedded 478-point face landmark resource.</summary>
    public const string Landmarks =
        "OpenPhysical.Biometrics.Onnx.Resources.face_landmarker_Nx3x256x256.onnx";
}
