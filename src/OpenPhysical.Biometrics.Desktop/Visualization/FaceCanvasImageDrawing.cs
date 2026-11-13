// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Media;

namespace OpenPhysical.Biometrics.Desktop;

internal static class FaceCanvasImageDrawing
{
    internal static bool Draw(DrawingContext context, FacePreviewScene scene, double scale)
    {
        context.DrawImage(scene.Image, new Rect(0, 0, scene.Width, scene.Height));
        return true;
    }
}
