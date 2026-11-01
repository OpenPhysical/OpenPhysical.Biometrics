// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

// Solves for the uniform crop that satisfies the Full Frontal profile, or reports the review finding.
internal static class PortraitCrop
{
    internal static Result<PortraitCropPlan, PortraitFinding> Calculate(
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
