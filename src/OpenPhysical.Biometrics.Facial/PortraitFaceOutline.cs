// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class PortraitFaceOutline
{
    internal static Result<ImmutableArray<PortraitPoint>, FacialError> Create(
        PortraitFace face,
        PortraitTransform transform
    )
    {
        if (
            face.Landmarks.Length != 478
            || face.Landmarks.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))
        )
            return FacialErrors.InvalidLandmarks();
        var points = face
            .Landmarks.Select(p => transform.Map(new(p.X, p.Y)))
            .Distinct()
            .OrderBy(p => p.X)
            .ThenBy(p => p.Y)
            .ToArray();
        if (points.Length < 3)
            return FacialErrors.InvalidLandmarks();
        var lower = Chain(points);
        var upper = Chain(points.Reverse());
        return lower.Take(lower.Count - 1).Concat(upper.Take(upper.Count - 1)).ToImmutableArray();
    }

    private static List<PortraitPoint> Chain(IEnumerable<PortraitPoint> points)
    {
        var result = new List<PortraitPoint>();
        foreach (var point in points)
        {
            while (result.Count > 1 && Cross(result[^2], result[^1], point) <= 0)
                result.RemoveAt(result.Count - 1);
            result.Add(point);
        }
        return result;
    }

    private static double Cross(PortraitPoint a, PortraitPoint b, PortraitPoint p) =>
        (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

    internal static bool Contains(ImmutableArray<PortraitPoint> outline, double x, double y)
    {
        var inside = false;
        for (var i = 0; i < outline.Length; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Length];
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }
}
