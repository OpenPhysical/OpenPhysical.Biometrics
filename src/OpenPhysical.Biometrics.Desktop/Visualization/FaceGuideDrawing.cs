// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Media;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;

namespace OpenPhysical.Biometrics.Desktop;

internal static class FaceGuideDrawing
{
    private static readonly IBrush red = new SolidColorBrush(
        Avalonia.Media.Color.FromRgb(0xB3, 0x26, 0x1E)
    );
    private static readonly IBrush green = new SolidColorBrush(
        Avalonia.Media.Color.FromRgb(0x50, 0x78, 0x52)
    );
    private static readonly IBrush blue = new SolidColorBrush(
        Avalonia.Media.Color.FromRgb(0x15, 0x65, 0xC0)
    );
    private static readonly Pen framing = new(red, 1);
    private static readonly Pen range = new(red, 1, DashStyle.Dash);
    private static readonly Pen region = new(green, 1);
    private static readonly Pen detection = new(blue, 1);

    internal static bool Draw(
        DrawingContext context,
        Maybe<FaceImageGuides> guides,
        Matrix transform,
        Size viewport
    )
    {
        if (guides.HasNoValue)
            return false;
        Point Map(PortraitPoint point) => transform.Transform(new Point(point.X, point.Y));
        using var clipped = context.PushClip(new Rect(viewport));
        foreach (var line in guides.Value.Segments)
            context.DrawLine(Stroke(line), Map(line.Start), Map(line.End));
        foreach (var point in guides.Value.DetectionPoints)
            context.DrawEllipse(blue, new Pen(Brushes.Transparent, 0), Map(point), 1.25, 1.25);
        return true;
    }

    private static Pen Stroke(FaceGuideSegment line) =>
        line.Tone switch
        {
            FaceGuideTone.Region => region,
            FaceGuideTone.Detection => detection,
            _ => line.IsDashed ? range : framing,
        };
}
