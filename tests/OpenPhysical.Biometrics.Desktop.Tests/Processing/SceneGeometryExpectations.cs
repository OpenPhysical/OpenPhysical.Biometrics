// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using Avalonia;

internal static class SceneGeometryExpectations
{
    internal static Rect MaskBounds(ImmutableArray<byte> mask, int width)
    {
        var included = Enumerable.Range(0, mask.Length).Where(index => mask[index] != 0).ToArray();
        var left = included.Min(index => index % width);
        var top = included.Min(index => index / width);
        return new Rect(
            left,
            top,
            included.Max(index => index % width) + 1 - left,
            included.Max(index => index / width) + 1 - top
        );
    }

    internal static Rect OutlineBounds(FingerprintScene scene)
    {
        var left = scene.Foreground.Min(edge => Math.Min(edge.X1, edge.X2));
        var top = scene.Foreground.Min(edge => Math.Min(edge.Y1, edge.Y2));
        return new Rect(
            left,
            top,
            scene.Foreground.Max(edge => Math.Max(edge.X1, edge.X2)) - left,
            scene.Foreground.Max(edge => Math.Max(edge.Y1, edge.Y2)) - top
        );
    }
}
