// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

internal sealed record AlignmentFootprint(double MinX, double MinY, double MaxX, double MaxY)
{
    internal static AlignmentFootprint Create(AlignmentPoint[] points) =>
        new(
            points.Min(point => point.X),
            points.Min(point => point.Y),
            points.Max(point => point.X),
            points.Max(point => point.Y)
        );

    internal bool Equivalent(
        FingerprintSimilarityTransform first,
        FingerprintSimilarityTransform second
    ) =>
        Separation(first, second, MinX, MinY) < 28
        && Separation(first, second, MaxX, MinY) < 28
        && Separation(first, second, MinX, MaxY) < 28
        && Separation(first, second, MaxX, MaxY) < 28;

    private static double Separation(
        FingerprintSimilarityTransform first,
        FingerprintSimilarityTransform second,
        double x,
        double y
    )
    {
        var dx =
            (first.A - second.A) * x
            - (first.B - second.B) * y
            + first.TranslationX
            - second.TranslationX;
        var dy =
            (first.B - second.B) * x
            + (first.A - second.A) * y
            + first.TranslationY
            - second.TranslationY;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
