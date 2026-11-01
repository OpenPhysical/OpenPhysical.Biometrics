// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Named anatomical features in orientation-corrected source pixels.</summary>
/// <param name="ViewerLeftEye">Midpoint of the left eye corners as viewed in the image.</param>
/// <param name="ViewerRightEye">Midpoint of the right eye corners as viewed in the image.</param>
/// <param name="NoseBridge">Nose bridge used for horizontal centering.</param>
/// <param name="MouthCenter">Midpoint of the inner upper and lower lip.</param>
/// <param name="Chin">Bottom of the mesh chin.</param>
/// <param name="Forehead">Upper mesh contour, distinct from the crown of the hair.</param>
public sealed record PortraitFeatures(
    PortraitPoint ViewerLeftEye,
    PortraitPoint ViewerRightEye,
    PortraitPoint NoseBridge,
    PortraitPoint MouthCenter,
    PortraitPoint Chin,
    PortraitPoint Forehead
)
{
    internal static Result<PortraitFeatures, FacialError> FromMediaPipe(
        ImmutableArray<FaceMeshPoint> points
    )
    {
        if (points.IsDefault || points.Length != 478)
            return FacialErrors.InvalidLandmarks().Fail<PortraitFeatures>();
        PortraitPoint At(int index) => new(points[index].X, points[index].Y);
        PortraitPoint Mid(int a, int b) =>
            new((points[a].X + points[b].X) / 2, (points[a].Y + points[b].Y) / 2);
        // These are anatomical correspondences in Google's canonical face mesh.
        return new PortraitFeatures(
            Mid(33, 133),
            Mid(362, 263),
            At(168),
            Mid(13, 14),
            At(152),
            At(10)
        ).Succeed();
    }
}
