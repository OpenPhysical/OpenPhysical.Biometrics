// SPDX-License-Identifier: GPL-2.0-or-later
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal static class FingerprintSceneDrawing
{
    internal static UnitResult<DesktopError> Draw(
        DrawingContext context,
        FingerprintScene scene,
        bool showSegmentation,
        bool showOverlay,
        bool invert = false,
        double scale = 1
    )
    {
        if (showSegmentation)
            DrawForeground(context, scene, invert);
        if (showOverlay)
            DrawFeatures(context, scene, invert, scale);
        return UnitResult.Success<DesktopError>();
    }

    private static UnitResult<DesktopError> DrawFeatures(
        DrawingContext context,
        FingerprintScene scene,
        bool invert,
        double scale
    )
    {
        foreach (var point in scene.Minutiae)
            MinutiaGlyph.Draw(
                context,
                point.X,
                point.Y,
                point.Type,
                point.Angle * 2,
                point.Quality >= 60
                    ? (invert ? Brushes.LimeGreen : Brushes.DarkGreen)
                    : (invert ? Brushes.Orange : Brushes.DarkOrange),
                invert,
                scale
            );
        foreach (var point in scene.ReferenceOverlay)
            MinutiaGlyph.Draw(
                context,
                point.X,
                point.Y,
                point.Type,
                point.AngleDegrees,
                point.Matched
                    ? (invert ? Brushes.Turquoise : Brushes.Teal)
                    : (invert ? Brushes.Orange : Brushes.DarkOrange),
                invert,
                scale
            );
        return UnitResult.Success<DesktopError>();
    }

    private static UnitResult<DesktopError> DrawForeground(
        DrawingContext context,
        FingerprintScene scene,
        bool invert
    )
    {
        var foreground = new Pen(invert ? Brushes.Turquoise : Brushes.Teal, 1.5);
        foreach (var edge in scene.Foreground)
            context.DrawLine(foreground, new Point(edge.X1, edge.Y1), new Point(edge.X2, edge.Y2));
        return UnitResult.Success<DesktopError>();
    }

    internal static UnitResult<DesktopError> DrawImage(
        DrawingContext context,
        FingerprintScene scene,
        DisplayBitmapCache display,
        bool invert
    )
    {
        var image = display.Select(scene, invert);
        if (image.IsSuccess)
        {
            image.Value.Tap(bitmap => context.DrawImage(bitmap, new Rect(bitmap.Size)));
            return UnitResult.Success<DesktopError>();
        }
        context.DrawText(
            new FormattedText(
                image.Error.Message,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                14,
                invert ? Brushes.White : Brushes.Black
            ),
            new Point(8, 8)
        );
        return UnitResult.Failure(image.Error);
    }
}
