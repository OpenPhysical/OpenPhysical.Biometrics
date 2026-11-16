// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

/// <summary>Plans source-supported uniform portrait crops without repeating face detection.</summary>
public static class PortraitCrop
{
    /// <summary>Calculates a uniform Full Frontal crop or reports the measurements requiring review.</summary>
    /// <param name="face">Detected face in oriented source coordinates.</param>
    /// <param name="features">Anatomical features derived from that face.</param>
    /// <param name="review">Optional operator measurements in the same source coordinates.</param>
    /// <param name="width">Oriented source width in pixels.</param>
    /// <param name="height">Oriented source height in pixels.</param>
    /// <returns>The source-to-portrait plan or a geometry review finding.</returns>
    public static Result<PortraitCropPlan, PortraitFinding> Calculate(
        PortraitFace face,
        PortraitFeatures features,
        Maybe<ReviewedPortraitGeometry> review,
        int width,
        int height
    )
    {
        var pose = PortraitSourcePose.Validate(face, features);
        return pose.IsFailure
            ? Result.Failure<PortraitCropPlan, PortraitFinding>(pose.Error)
            : PortraitFraming.Calculate(face, features, review, width, height);
    }
}
