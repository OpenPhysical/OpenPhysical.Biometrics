// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using OpenPhysical.Biometrics.Capture.Camera;
using OpenPhysical.Biometrics.Facial;

internal static class FaceLiveGuidance
{
    internal static string SourceFailure(CameraError error) =>
        error.Code switch
        {
            CameraErrorCodes.InvalidFrame =>
                "This camera did not provide a usable image. Choose another camera or check its software.",
            CameraErrorCodes.FrameTimeout =>
                "The camera stopped sending images. Reconnect it or choose another camera.",
            _ => error.Message,
        };

    internal static FaceCaptureReadiness Assess(
        PortraitAnalysis analysis,
        PortraitMouthPolicy mouthPolicy
    )
    {
        if (analysis.Faces.Length != 1)
            return new(
                FaceReadinessState.FaceCount,
                analysis.Faces.IsEmpty
                    ? "Position your face in view."
                    : "Only one person should be in view."
            );
        var face = analysis.Faces[0];
        if (face.Features.HasNoValue)
            return new(
                FaceReadinessState.Landmarks,
                "Look straight at the camera with your whole head visible."
            );
        var pose = PortraitSourcePose.Validate(face, face.Features.Value);
        if (pose.IsFailure)
            return new(FaceReadinessState.Pose, ForPose(pose.Error));
        var mouth = PortraitMouth.Assess(face, mouthPolicy);
        if (mouth.IsFailure)
            return new(
                FaceReadinessState.Mouth,
                mouth.Error.Code == "MouthOpen"
                    ? "Close your lips gently."
                    : "Keep your whole face visible and look straight at the camera."
            );
        return ForFraming(analysis, face);
    }

    private static FaceCaptureReadiness ForFraming(PortraitAnalysis analysis, PortraitFace face)
    {
        var geometry = new ReviewedPortraitGeometry(
            1,
            analysis.SourceSha256,
            analysis.Width,
            analysis.Height,
            face.Index
        );
        var crop = PortraitCrop.Calculate(
            face,
            face.Features.Value,
            geometry,
            analysis.Width,
            analysis.Height
        );
        return crop.IsSuccess
            ? new(FaceReadinessState.Ready, "Look straight at the camera. Capture when ready.")
            : new(
                FaceReadinessState.Framing,
                crop.Error.Code == "SourceCoverage"
                    ? "Move back so your whole head and the surrounding background fit."
                    : "Center your face and look straight at the camera."
            );
    }

    private static string ForPose(PortraitFinding finding) =>
        finding.Code switch
        {
            "SourceYaw" => "Turn your face straight toward the camera.",
            "SourcePitch" => "Bring the camera to eye level and look straight ahead.",
            "SourceRoll" => "Keep your head upright and look straight at the camera.",
            _ => "Keep your whole face visible and look straight at the camera.",
        };
}
