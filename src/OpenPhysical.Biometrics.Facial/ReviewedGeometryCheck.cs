// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

// Confirms that reviewed measurements belong to this exact source and face selection.
internal static class ReviewedGeometryCheck
{
    // Reviewed geometry fixes the face selection; otherwise the caller's selection stands.
    internal static Result<Maybe<int>, FacialError> Resolve(
        Maybe<ReviewedPortraitGeometry> geometry,
        PortraitSource source,
        Maybe<int> faceIndex
    ) =>
        geometry.HasNoValue
            ? faceIndex.Succeed()
            : Validate(geometry.Value, source, faceIndex)
                .Map(() => Maybe.From(geometry.Value.FaceIndex));

    private static UnitResult<FacialError> Validate(
        ReviewedPortraitGeometry geometry,
        PortraitSource source,
        Maybe<int> faceIndex
    )
    {
        var image = source.Image;
        if (
            geometry.Version != 1
            || !string.Equals(
                source.Hash,
                geometry.SourceSha256,
                StringComparison.OrdinalIgnoreCase
            )
            || geometry.Width != image.Width
            || geometry.Height != image.Height
            || geometry.FaceIndex < 0
            || (faceIndex.HasValue && faceIndex.Value != geometry.FaceIndex)
        )
            return UnitResult.Failure(FacialErrors.GeometryMismatch());
        return Points(geometry).All(point => Inside(point, image))
            ? UnitResult.Success<FacialError>()
            : UnitResult.Failure(FacialErrors.GeometryPointInvalid());
    }

    private static IEnumerable<PortraitPoint> Points(ReviewedPortraitGeometry geometry) =>
        new[] { geometry.Crown, geometry.Chin, geometry.ViewerLeftEar, geometry.ViewerRightEar }
            .Where(point => point.HasValue)
            .Select(point => point.Value);

    private static bool Inside(PortraitPoint point, PortraitPixels image) =>
        double.IsFinite(point.X)
        && double.IsFinite(point.Y)
        && point.X >= 0
        && point.Y >= 0
        && point.X < image.Width
        && point.Y < image.Height;
}
