// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Media;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal static class MinutiaGlyph
{
    private static readonly Geometry Ending = Shape(1);
    private static readonly Geometry Bifurcation = Shape(2);
    private static readonly Geometry Unknown = Shape(0);
    private static readonly Pen[] LightHalo = Halo(Colors.White);
    private static readonly Pen[] DarkHalo = Halo(Colors.Black);

    internal static UnitResult<DesktopError> Draw(
        DrawingContext context,
        double x,
        double y,
        byte type,
        double angleDegrees,
        IBrush brush,
        bool invert,
        double scale
    )
    {
        var geometry =
            type == 1 ? Ending
            : type == 2 ? Bifurcation
            : Unknown;
        using var transform = context.PushTransform(
            Matrix.CreateScale(1 / scale, 1 / scale)
                * Matrix.CreateRotation(-angleDegrees * Math.PI / 180)
                * Matrix.CreateTranslation(x + .5, y + .5)
        );
        foreach (var halo in invert ? DarkHalo : LightHalo)
            context.DrawGeometry(Brushes.Transparent, halo, geometry);
        context.DrawGeometry(
            Brushes.Transparent,
            new Pen(brush, 1.1, lineCap: PenLineCap.Round),
            geometry
        );
        return UnitResult.Success<DesktopError>();
    }

    private static Geometry Shape(byte type)
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        if (type == 0)
        {
            Line(path, new(-3, -3), new(3, 3));
            Line(path, new(-3, 3), new(3, -3));
            Line(path, default, new(13, 0));
            return geometry;
        }
        path.BeginFigure(new(-4, 0), false);
        path.ArcTo(new(4, 0), new(4, 4), 0, false, SweepDirection.Clockwise);
        path.ArcTo(new(-4, 0), new(4, 4), 0, false, SweepDirection.Clockwise);
        path.EndFigure(true);
        if (type == 2)
        {
            // The two ticks symbolize a fork around the one recorded direction.
            Line(path, new(7, -2.2), new(13, -4));
            Line(path, new(7, 2.2), new(13, 4));
        }
        else
            Line(path, new(7, 0), new(13, 0));
        return geometry;
    }

    private static UnitResult<DesktopError> Line(StreamGeometryContext path, Point start, Point end)
    {
        path.BeginFigure(start, false);
        path.LineTo(end);
        path.EndFigure(false);
        return UnitResult.Success<DesktopError>();
    }

    private static Pen[] Halo(Color color) =>
        [HaloPen(color, 4.5, .1), HaloPen(color, 3.25, .2), HaloPen(color, 2.25, .5)];

    private static Pen HaloPen(Color color, double width, double opacity) =>
        new(new SolidColorBrush(color, opacity), width, lineCap: PenLineCap.Round);
}
