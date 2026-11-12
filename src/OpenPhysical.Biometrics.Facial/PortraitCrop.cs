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
        var frame = PortraitHeadFrame.From(face, features, review);
        if (frame.IsDegenerate)
            return Review(
                "HeadGeometry",
                "Review head boundaries or supply a higher-resolution capture."
            );
        // A uniform transform preserves proportions. Bound scale before solving source coverage.
        var minScale = 240 / frame.HeadWidth;
        var maxScale = Math.Min(
            1,
            Math.Min(480 * 4.0 / (7 * frame.HeadWidth), 512 / frame.HeadHeight)
        );
        if (minScale > maxScale)
            return Review(
                "HeadProportions",
                "Head proportions require review to fit the portrait profile."
            );
        var region = PortraitCropConstraints.Apply(
            FeasibleRegion.Initial(minScale, maxScale),
            frame,
            width,
            height
        );
        return region.IsEmpty
            ? Review(
                "SourceCoverage",
                "A centered full-head crop requires additional source coverage or resolution."
            )
            : Result.Success<PortraitCropPlan, PortraitFinding>(
                PortraitCropPlan.Create(frame, region, review)
            );
    }

    private static Result<PortraitCropPlan, PortraitFinding> Review(string code, string message) =>
        Result.Failure<PortraitCropPlan, PortraitFinding>(new PortraitFinding(code, message));
}
