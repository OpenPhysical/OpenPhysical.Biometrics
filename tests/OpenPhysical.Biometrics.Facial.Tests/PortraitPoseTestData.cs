// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class PortraitPoseTestData
{
    internal static (PortraitFace Face, ReviewedPortraitGeometry Review) Rotated(double degrees)
    {
        var (face, review) = TestFixtures.Geometry();
        var angle = degrees * Math.PI / 180;
        var rotate = new PortraitTransform(Math.Cos(angle), Math.Sin(angle), 80, 150);
        Maybe<PortraitPoint> Map(Maybe<PortraitPoint> point) => point.Select(rotate.Map);
        return (
            face with
            {
                Landmarks = face
                    .Landmarks.Select(point =>
                    {
                        var rotated = rotate.Map(new(point.X, point.Y));
                        return new FaceMeshPoint(rotated.X, rotated.Y, point.Z);
                    })
                    .ToImmutableArray(),
                ViewerLeftEar = rotate.Map(face.ViewerLeftEar),
                ViewerRightEar = rotate.Map(face.ViewerRightEar),
                Pose = new PortraitPoseEstimate(0, 0, degrees, 63),
            },
            review with
            {
                Crown = Map(review.Crown),
                Chin = Map(review.Chin),
                ViewerLeftEar = Map(review.ViewerLeftEar),
                ViewerRightEar = Map(review.ViewerRightEar),
            }
        );
    }
}
