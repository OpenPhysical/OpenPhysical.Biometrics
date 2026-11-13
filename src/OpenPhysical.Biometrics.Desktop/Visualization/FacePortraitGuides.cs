// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Piv;

namespace OpenPhysical.Biometrics.Desktop;

internal static class FacePortraitGuides
{
    internal static Maybe<FaceImageGuides> Create(
        string hash,
        PortraitTransform transform,
        PortraitMeasurements measurements,
        PortraitFeatures features,
        bool sourceCoordinates
    )
    {
        var assessed = PivPortraitGeometry.Assess(
            PortraitOutput.Width,
            PortraitOutput.Height,
            measurements.HeadWidth,
            measurements.HeadHeight,
            measurements.EyeHeightFraction
        );
        if (assessed.IsFailure || transform.Inverse(new(0, 0)).IsFailure)
            return default;
        var guides = new FaceImageGuides(hash, false, Lines(transform, measurements, features), []);
        return sourceCoordinates ? FaceGuideGeometry.Map(guides, transform) : guides;
    }

    private static ImmutableArray<FaceGuideSegment> Lines(
        PortraitTransform transform,
        PortraitMeasurements measurements,
        PortraitFeatures features
    )
    {
        const int width = PortraitOutput.Width;
        const int height = PortraitOutput.Height;
        var lines = FaceGuideGeometry
            .Rectangle(0, 0, width, height, FaceGuideTone.Framing)
            .ToBuilder();
        lines.AddRange(FaceGuideDimensions.Create(width, height));
        lines.AddRange(SuggestedRegion(width, height));
        lines.Add(new(new(width / 2.0, 0), new(width / 2.0, height), FaceGuideTone.Framing));
        foreach (
            var fraction in new[]
            {
                PivPortraitGeometry.MinimumEyeHeightFraction,
                PivPortraitGeometry.MaximumEyeHeightFraction,
            }
        )
            lines.Add(
                new(new(0, height * fraction), new(width, height * fraction), FaceGuideTone.Framing)
                {
                    IsDashed = true,
                }
            );
        var eyeY = height * measurements.EyeHeightFraction;
        lines.Add(new(new(0, eyeY), new(width, eyeY), FaceGuideTone.Framing));
        lines.AddRange(Crosshair(transform.Map(features.ViewerLeftEye)));
        lines.AddRange(Crosshair(transform.Map(features.ViewerRightEye)));
        return lines.ToImmutable();
    }

    private static ImmutableArray<FaceGuideSegment> Crosshair(PortraitPoint point) =>
        [
            new(new(point.X - 12, point.Y), new(point.X + 12, point.Y), FaceGuideTone.Framing),
            new(new(point.X, point.Y - 12), new(point.X, point.Y + 12), FaceGuideTone.Framing),
        ];

    private static ImmutableArray<FaceGuideSegment> SuggestedRegion(int width, int height) =>
        // Proportional visual aid from the informative token-image example, not an output crop.
        FaceGuideGeometry.Rectangle(
            width * .10,
            height * .075,
            width * .90,
            height * .825,
            FaceGuideTone.Region
        );
}
