// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Piv;

using CSharpFunctionalExtensions;

/// <summary>Shares PIV portrait framing limits without claiming physical camera qualification.</summary>
public static class PivPortraitGeometry
{
    /// <summary>Conservative preparation policy for ear-attachment width, not proof of optical resolution.</summary>
    public const int MinimumHeadWidth = 240;

    /// <summary>SP 800-76-2 Table 12 requires image width to exceed this number of pixels.</summary>
    public const int MinimumImageWidthExclusive = 420;

    /// <summary>Numerator of the INCITS 385 section 8.3.4 minimum image-to-head width ratio.</summary>
    public const int HeadWidthRatioNumerator = 7;

    /// <summary>Denominator of the INCITS 385 section 8.3.4 minimum image-to-head width ratio.</summary>
    public const int HeadWidthRatioDenominator = 4;

    /// <summary>Minimum image-to-ear-attachment-width ratio.</summary>
    public const double MinimumImageToHeadWidthRatio =
        (double)HeadWidthRatioNumerator / HeadWidthRatioDenominator;

    /// <summary>Minimum eye position measured down from the image top.</summary>
    public const double MinimumEyeHeightFraction = .3;

    /// <summary>Maximum eye position measured down from the image top.</summary>
    public const double MaximumEyeHeightFraction = .5;

    /// <summary>Maximum crown-to-chin extent as a fraction of image height.</summary>
    public const double MaximumHeadHeightFraction = .8;

    /// <summary>INCITS 385 section 7.2.2 requires absolute pitch, yaw, and roll to be strictly below five degrees.</summary>
    public const double MaximumPoseDeviationDegrees = 5;

    /// <summary>Checks supplied pixel measurements, independently of their review or optical provenance.</summary>
    /// <param name="imageWidth">Output image width in pixels.</param>
    /// <param name="imageHeight">Output image height in pixels.</param>
    /// <param name="headWidth">Ear-attachment separation in output pixels.</param>
    /// <param name="headHeight">Crown-to-chin extent in output pixels.</param>
    /// <param name="eyeHeightFraction">Eye position measured down from the output top.</param>
    /// <returns>Individual numeric checks, or an error for invalid measurements.</returns>
    public static Result<PivPortraitGeometryAssessment, PivError> Assess(
        int imageWidth,
        int imageHeight,
        double headWidth,
        double headHeight,
        double eyeHeightFraction
    )
    {
        if (
            imageWidth <= 0
            || imageHeight <= 0
            || !double.IsFinite(headWidth)
            || headWidth <= 0
            || !double.IsFinite(headHeight)
            || headHeight <= 0
            || !double.IsFinite(eyeHeightFraction)
        )
            return Result.Failure<PivPortraitGeometryAssessment, PivError>(
                new(PivErrorCodes.InvalidPortraitMeasurements, "Portrait measurements are invalid.")
            );
        return new PivPortraitGeometryAssessment(
            HasSupportedImageDimensions(imageWidth, imageHeight),
            headWidth >= MinimumHeadWidth,
            imageWidth / headWidth >= MinimumImageToHeadWidthRatio,
            eyeHeightFraction >= MinimumEyeHeightFraction
                && eyeHeightFraction <= MaximumEyeHeightFraction,
            headHeight <= imageHeight * MaximumHeadHeightFraction
        );
    }

    internal static bool HasSupportedImageDimensions(int width, int height) =>
        width > MinimumImageWidthExclusive && height > 0;
}
