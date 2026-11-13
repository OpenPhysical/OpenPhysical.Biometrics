// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using Avalonia;

internal static class ImageViewportTransform
{
    internal static (double Scale, Vector Offset) Fit(
        Size viewport,
        Rect region,
        double zoom,
        Vector pan,
        double padding
    )
    {
        padding = Math.Min(padding, Math.Min(viewport.Width, viewport.Height) / 4);
        var scale =
            Math.Min(
                (viewport.Width - padding * 2) / region.Width,
                (viewport.Height - padding * 2) / region.Height
            ) * zoom;
        return (
            scale,
            new Vector(
                viewport.Width / 2 - region.Center.X * scale,
                viewport.Height / 2 - region.Center.Y * scale
            ) + pan
        );
    }
}
