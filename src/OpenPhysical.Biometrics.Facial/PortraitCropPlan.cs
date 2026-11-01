// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

// The selected source-to-portrait transform and the measurements that justify it.
internal sealed record PortraitCropPlan(
    PortraitTransform Transform,
    PortraitMeasurements Measurements
)
{
    internal static PortraitCropPlan Create(
        PortraitHeadFrame frame,
        FeasibleRegion region,
        Maybe<ReviewedPortraitGeometry> review
    )
    {
        var (scale, eyeY) = PortraitTarget.Nearest(region, frame.HeadWidth);
        var rotation = frame.Rotation;
        var transform = new PortraitTransform(
            rotation.A * scale,
            rotation.B * scale,
            240 - frame.Center * scale,
            eyeY - frame.Eye.Y * scale
        );
        var crown = review.Bind(r => r.Crown);
        var left = review.Bind(r => r.ViewerLeftEar);
        var right = review.Bind(r => r.ViewerRightEar);
        var measures = new PortraitMeasurements(
            scale * frame.HeadWidth,
            scale * frame.HeadHeight,
            eyeY / 640,
            crown.HasValue && left.HasValue && right.HasValue
        )
        {
            CrownOrigin = Origin(crown, PortraitMeasurementOrigin.Estimated),
            ViewerLeftEarOrigin = Origin(left, PortraitMeasurementOrigin.Estimated),
            ViewerRightEarOrigin = Origin(right, PortraitMeasurementOrigin.Estimated),
            ChinOrigin = Origin(review.Bind(r => r.Chin), PortraitMeasurementOrigin.Mesh),
        };
        return new(transform, measures);
    }

    private static PortraitMeasurementOrigin Origin(
        Maybe<PortraitPoint> supplied,
        PortraitMeasurementOrigin automatic
    ) => supplied.HasValue ? PortraitMeasurementOrigin.Reviewed : automatic;
}
