// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Piv;

/// <summary>Plans source-supported portrait framing for inspection without accepting source pose.</summary>
public static class PortraitFraming
{
    /// <summary>Calculates one uniform framing plan independently of automated acceptance checks.</summary>
    /// <param name="face">Detected face in oriented source coordinates.</param>
    /// <param name="features">Anatomical features derived from the same face.</param>
    /// <param name="review">Optional operator measurements in the same coordinates.</param>
    /// <param name="width">Oriented source width in pixels.</param>
    /// <param name="height">Oriented source height in pixels.</param>
    /// <returns>A source-supported plan or the geometry finding that prevents framing.</returns>
    public static Result<PortraitCropPlan, PortraitFinding> Calculate(
        PortraitFace face,
        PortraitFeatures features,
        Maybe<ReviewedPortraitGeometry> review,
        int width,
        int height
    ) => Calculate(PortraitHeadFrame.From(face, features, review), review, width, height);

    private static Result<PortraitCropPlan, PortraitFinding> Calculate(
        PortraitHeadFrame frame,
        Maybe<ReviewedPortraitGeometry> review,
        int width,
        int height
    )
    {
        if (frame.IsDegenerate)
            return Review(
                "HeadGeometry",
                "Review head boundaries or supply a higher-resolution capture."
            );
        var minScale = PivPortraitGeometry.MinimumHeadWidth / frame.HeadWidth;
        var maxScale = MaximumScale(frame);
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

    private static double MaximumScale(PortraitHeadFrame frame) =>
        Math.Min(
            1,
            Math.Min(
                PortraitOutput.Width
                    * (double)PivPortraitGeometry.HeadWidthRatioDenominator
                    / (PivPortraitGeometry.HeadWidthRatioNumerator * frame.HeadWidth),
                PortraitOutput.Height
                    * PivPortraitGeometry.MaximumHeadHeightFraction
                    / frame.HeadHeight
            )
        );

    private static Result<PortraitCropPlan, PortraitFinding> Review(string code, string message) =>
        Result.Failure<PortraitCropPlan, PortraitFinding>(new PortraitFinding(code, message));
}
