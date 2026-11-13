// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;

namespace OpenPhysical.Biometrics.Desktop;

internal static class FaceGuideFactory
{
    internal static Maybe<FaceImageGuides> Create(
        PortraitPreparationResult portrait,
        bool sourceCoordinates
    )
    {
        if (portrait.Faces.Length != 1)
            return default;
        return portrait
            .Faces[0]
            .Features.Bind(features =>
                portrait.Transform.Bind(transform =>
                    portrait.Measurements.Bind(measurements =>
                        FacePortraitGuides.Create(
                            portrait.SourceSha256,
                            transform,
                            measurements,
                            features,
                            sourceCoordinates
                        )
                    )
                )
            );
    }

    internal static Maybe<FaceImageGuides> Detection(PortraitAnalysis analysis) =>
        Detect(analysis.SourceSha256, analysis.Faces, point => point, true);

    internal static Maybe<FaceImageGuides> Detection(
        PortraitPreparationResult portrait,
        bool sourceCoordinates
    ) =>
        sourceCoordinates
            ? Detect(portrait.SourceSha256, portrait.Faces, point => point, true)
            : portrait.Transform.Bind(transform =>
                Detect(portrait.SourceSha256, portrait.Faces, transform.Map, false)
            );

    private static Maybe<FaceImageGuides> Detect(
        string hash,
        ImmutableArray<PortraitFace> faces,
        Func<PortraitPoint, PortraitPoint> map,
        bool sourceCoordinates
    )
    {
        var points = faces
            .SelectMany(face => face.Landmarks)
            .Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y))
            .Select(point => map(new(point.X, point.Y)))
            .ToImmutableArray();
        if (points.IsEmpty)
            return default;
        var left = points.Min(point => point.X);
        var right = points.Max(point => point.X);
        var top = points.Min(point => point.Y);
        var bottom = points.Max(point => point.Y);
        var segments = FaceGuideGeometry.Rectangle(
            left,
            top,
            right,
            bottom,
            FaceGuideTone.Detection
        );
        return new FaceImageGuides(hash, sourceCoordinates, segments, points);
    }
}
