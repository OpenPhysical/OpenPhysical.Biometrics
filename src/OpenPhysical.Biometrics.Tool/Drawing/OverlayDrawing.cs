// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Drawing;

using System.Drawing;
using OpenPhysical.Biometrics.Imaging;
using SkiaSharp;

internal sealed record EllipsePolygon(float X, float Y, float Radius);

internal sealed record LinearLineSegment(params PointF[] Points);

internal sealed record OverlayPath(LinearLineSegment Segment);

internal enum OverlayFontStyle
{
    Regular,
    Bold,
}

internal sealed record OverlayFont(string Family, float Size, OverlayFontStyle Style);

internal static class OverlayFonts
{
    internal static OverlayFont CreateFont(
        string family,
        float size,
        OverlayFontStyle style = OverlayFontStyle.Regular
    ) => new(family, size, style);
}

internal static class OverlayColors
{
    internal static Color FromRgba(byte r, byte g, byte b, byte a) => Color.FromArgb(a, r, g, b);
}

// CLI overlays operate on an owned copy. Skia drawing objects stay scoped here;
// biometric processing and inspection contracts retain their ordinary .NET types.
internal static class OverlayDrawing
{
    internal static Color WithAlpha(this Color color, float opacity) =>
        Color.FromArgb(
            (int)Math.Clamp(Math.Round(opacity * 255), 0, 255),
            color.R,
            color.G,
            color.B
        );

    internal static RgbImage Fill(this RgbImage image, Color color, RectangleF rectangle) =>
        Paint(
            image,
            canvas =>
            {
                using var paint = Pen(color, 0, false);
                canvas.DrawRect(Rect(rectangle), paint);
            }
        );

    internal static RgbImage Fill(this RgbImage image, Color color, EllipsePolygon ellipse) =>
        Paint(
            image,
            canvas =>
            {
                using var paint = Pen(color, 0, false);
                canvas.DrawCircle(ellipse.X, ellipse.Y, ellipse.Radius, paint);
            }
        );

    internal static RgbImage Draw(
        this RgbImage image,
        Color color,
        float width,
        RectangleF rectangle
    ) =>
        Paint(
            image,
            canvas =>
            {
                using var paint = Pen(color, width, true);
                canvas.DrawRect(Rect(rectangle), paint);
            }
        );

    internal static RgbImage Draw(
        this RgbImage image,
        Color color,
        float width,
        Rectangle rectangle
    ) => Draw(image, color, width, (RectangleF)rectangle);

    internal static RgbImage Draw(
        this RgbImage image,
        Color color,
        float width,
        OverlayPath path
    ) => DrawPoints(image, color, width, path.Segment.Points, false);

    internal static RgbImage DrawPolygon(
        this RgbImage image,
        Color color,
        float width,
        params PointF[] points
    ) => DrawPoints(image, color, width, points, true);

    internal static RgbImage DrawText(
        this RgbImage image,
        string text,
        OverlayFont font,
        Color color,
        PointF position
    ) =>
        Paint(
            image,
            canvas =>
            {
                using var paint = Pen(color, 0, false);
                using var typeface = SKTypeface.FromFamilyName(
                    font.Family,
                    font.Style == OverlayFontStyle.Bold ? SKFontStyle.Bold : SKFontStyle.Normal
                );
                using var nativeFont = new SKFont(typeface, font.Size);
                canvas.DrawText(
                    text,
                    position.X,
                    position.Y + font.Size,
                    SKTextAlign.Left,
                    nativeFont,
                    paint
                );
            }
        );

    private static RgbImage DrawPoints(
        RgbImage image,
        Color color,
        float width,
        PointF[] points,
        bool close
    ) =>
        Paint(
            image,
            canvas =>
            {
                if (points.Length == 0)
                    return;
                using var path = new SKPath();
                path.MoveTo(points[0].X, points[0].Y);
                foreach (var point in points.Skip(1))
                    path.LineTo(point.X, point.Y);
                if (close)
                    path.Close();
                using var paint = Pen(color, width, true);
                canvas.DrawPath(path, paint);
            }
        );

    private static SKPaint Pen(Color color, float width, bool stroke) =>
        new()
        {
            Color = new SKColor(color.R, color.G, color.B, color.A),
            Style = stroke ? SKPaintStyle.Stroke : SKPaintStyle.Fill,
            StrokeWidth = width,
            IsAntialias = true,
        };

    private static SKRect Rect(RectangleF rectangle) =>
        SKRect.Create(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

    private static RgbImage Paint(RgbImage image, Action<SKCanvas> action)
    {
        using var source = image.ToSkiaBitmap();
        using var nativeImage = SKImage.FromBitmap(source);
        using var bitmap = new SKBitmap(
            new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul)
        );
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(nativeImage, 0, 0, SKSamplingOptions.Default);
            action(canvas);
            canvas.Flush();
        }
        using var drawn = RgbImage.FromSkiaBitmap(bitmap);
        for (var y = 0; y < image.Height; y++)
            drawn.GetRowSpan(y).CopyTo(image.GetRowSpan(y));
        return image;
    }
}
