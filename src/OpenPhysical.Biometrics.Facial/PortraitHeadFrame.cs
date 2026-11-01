// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

// Head landmarks after rolling the source so the eyes are level.
internal sealed record PortraitHeadFrame(
    PortraitTransform Rotation,
    PortraitPoint Eye,
    PortraitPoint Chin,
    PortraitPoint Nose,
    PortraitPoint Mouth,
    PortraitPoint Left,
    PortraitPoint Right,
    PortraitPoint Crown
)
{
    internal double HeadWidth => Right.X - Left.X;

    internal double HeadHeight => Chin.Y - Crown.Y;

    internal double Center => (Nose.X + Mouth.X) / 2;

    internal bool IsDegenerate =>
        HeadWidth < 240 || HeadHeight <= 0 || Crown.Y >= Eye.Y || Eye.Y >= Chin.Y;

    internal static PortraitHeadFrame From(
        PortraitFace face,
        PortraitFeatures features,
        Maybe<ReviewedPortraitGeometry> review
    )
    {
        var leftEye = features.ViewerLeftEye;
        var rightEye = features.ViewerRightEye;
        var angle = Math.Atan2(rightEye.Y - leftEye.Y, rightEye.X - leftEye.X);
        var rotation = new PortraitTransform(Math.Cos(angle), Math.Sin(angle), 0, 0);
        var chin = rotation.Map(review.Bind(r => r.Chin).GetValueOrDefault(features.Chin));
        var eye = rotation.Map(
            new PortraitPoint((leftEye.X + rightEye.X) / 2, (leftEye.Y + rightEye.Y) / 2)
        );
        return new PortraitHeadFrame(
            rotation,
            eye,
            chin,
            rotation.Map(features.NoseBridge),
            rotation.Map(features.MouthCenter),
            rotation.Map(review.Bind(r => r.ViewerLeftEar).GetValueOrDefault(face.ViewerLeftEar)),
            rotation.Map(review.Bind(r => r.ViewerRightEar).GetValueOrDefault(face.ViewerRightEar)),
            CrownFrom(rotation, eye, chin, review)
        );
    }

    // Estimate the crown from the eye-to-chin distance and retain its estimated origin.
    private static PortraitPoint CrownFrom(
        PortraitTransform rotation,
        PortraitPoint eye,
        PortraitPoint chin,
        Maybe<ReviewedPortraitGeometry> review
    ) =>
        review
            .Bind(r => r.Crown)
            .Match(
                supplied => rotation.Map(supplied),
                () => new PortraitPoint(eye.X, eye.Y - (chin.Y - eye.Y) * .9)
            );
}
