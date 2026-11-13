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
            : Validate(
                    geometry.Value,
                    source.Hash,
                    source.Image.Width,
                    source.Image.Height,
                    faceIndex
                )
                .Map(() => Maybe.From(geometry.Value.FaceIndex));

    internal static UnitResult<FacialError> Validate(
        ReviewedPortraitGeometry geometry,
        string hash,
        int width,
        int height,
        Maybe<int> faceIndex
    )
    {
        if (
            geometry.Version != 1
            || !string.Equals(hash, geometry.SourceSha256, StringComparison.OrdinalIgnoreCase)
            || geometry.Width != width
            || geometry.Height != height
            || geometry.FaceIndex < 0
            || (faceIndex.HasValue && faceIndex.Value != geometry.FaceIndex)
        )
            return UnitResult.Failure(FacialErrors.GeometryMismatch());
        return Points(geometry).All(point => Inside(point, width, height))
            ? UnitResult.Success<FacialError>()
            : UnitResult.Failure(FacialErrors.GeometryPointInvalid());
    }

    private static IEnumerable<PortraitPoint> Points(ReviewedPortraitGeometry geometry) =>
        new[] { geometry.Crown, geometry.Chin, geometry.ViewerLeftEar, geometry.ViewerRightEar }
            .Where(point => point.HasValue)
            .Select(point => point.Value);

    private static bool Inside(PortraitPoint point, int width, int height) =>
        double.IsFinite(point.X)
        && double.IsFinite(point.Y)
        && point.X >= 0
        && point.Y >= 0
        && point.X < width
        && point.Y < height;
}
