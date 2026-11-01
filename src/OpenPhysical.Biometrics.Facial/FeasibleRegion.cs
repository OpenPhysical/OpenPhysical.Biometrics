// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;

// Feasible scale and eye-height pairs form a convex polygon. Clipping the
// polygon avoids missing a narrow feasible crop between sampled scales.
internal sealed record FeasibleRegion(ImmutableArray<(double Scale, double Eye)> Vertices)
{
    internal bool IsEmpty => Vertices.IsEmpty;

    internal static FeasibleRegion Initial(double minScale, double maxScale) =>
        new([(minScale, 192), (maxScale, 192), (maxScale, 320), (minScale, 320)]);

    // Keeps the half-plane a * scale + b * eye <= limit.
    internal FeasibleRegion Clip(double a, double b, double limit)
    {
        var output = ImmutableArray.CreateBuilder<(double Scale, double Eye)>();
        for (var i = 0; i < Vertices.Length; i++)
        {
            var start = Vertices[i];
            var end = Vertices[(i + 1) % Vertices.Length];
            var first = a * start.Scale + b * start.Eye - limit;
            var last = a * end.Scale + b * end.Eye - limit;
            if (first <= 0)
                output.Add(start);
            if ((first < 0 && last > 0) || (first > 0 && last < 0))
            {
                var fraction = first / (first - last);
                output.Add(
                    (
                        start.Scale + fraction * (end.Scale - start.Scale),
                        start.Eye + fraction * (end.Eye - start.Eye)
                    )
                );
            }
        }
        return new(output.ToImmutable());
    }
}
