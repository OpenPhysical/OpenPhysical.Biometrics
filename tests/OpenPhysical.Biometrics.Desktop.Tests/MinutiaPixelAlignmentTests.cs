// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;

[TestFixture]
internal sealed class MinutiaPixelAlignmentTests
{
    [AvaloniaTest]
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void CircleCentersMeetRenderedPixelCentersWithoutChangingTheTemplate(
        bool invert,
        bool aligned
    )
    {
        var record = Record();
        var encoded = Incits378Codec.Write(record).Value;
        using var scene = Impulse(record, aligned);
        using var surface = new ImageViewTestSurface(scene);
        surface.View.Invert = invert;
        for (var i = 0; i < 20; i++)
            surface.Press("Zoom in");
        surface.View.ShowFeatures = false;
        var impulse = ImpulseCenter(surface, invert);
        surface.View.ShowFeatures = true;
        var ring = RingCenter(surface);
        ring.X.Should().BeApproximately(impulse.X, 1);
        ring.Y.Should().BeApproximately(impulse.Y, 1);
        Incits378Codec.Write(record).Value.Should().Equal(encoded);
        var label =
            $"pixel-center-{(invert ? "inverted" : "normal")}-{(aligned ? "reference" : "canonical")}";
        WorkspaceReviewImages.Save(surface.Window, new Size(420, 360), label);
    }

    private static FingerprintRecord Record() =>
        new(0x0103, 1, 0x8001, 33, 33, 197, 197, [new(2, 0, 0, 80, [new(1, 16, 16, 0, 100)], [])]);

    private static FingerprintScene Impulse(FingerprintRecord record, bool aligned)
    {
        var point = record.Views[0].Minutiae[0];
        var pixels = Enumerable
            .Range(0, 33 * 33)
            .Select(i => i == 16 * 33 + 16 ? (byte)255 : (byte)40)
            .ToImmutableArray();
        return new FingerprintScene(33, 33, aligned ? [] : [point], Maybe.From(Bitmap(pixels)))
        {
            GrayPixels = pixels,
            ReferenceOverlay = aligned
                ? [new(point.X, point.Y, point.Angle * 2, point.Type, point.Quality, true)]
                : [],
        };
    }

    private static WriteableBitmap Bitmap(ImmutableArray<byte> pixels)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(33, 33),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque
        );
        using var locked = bitmap.Lock();
        var bgra = new byte[33 * 33 * 4];
        for (var i = 0; i < 33 * 33; i++)
        {
            var value = pixels[i];
            bgra[i * 4] = bgra[i * 4 + 1] = bgra[i * 4 + 2] = value;
            bgra[i * 4 + 3] = 255;
        }
        Marshal.Copy(bgra, 0, locked.Address, bgra.Length);
        return bitmap;
    }

    private static Point ImpulseCenter(ImageViewTestSurface surface, bool invert)
    {
        var (size, pixels) = surface.RenderPixels();
        var origin = surface.Image.TranslatePoint(default, surface.Window)!.Value;
        var area = new Rect(origin, surface.Image.Bounds.Size).Deflate(2);
        double weight = 0,
            xSum = 0,
            ySum = 0;
        for (var y = (int)Math.Ceiling(area.Top); y < area.Bottom; y++)
        for (var x = (int)Math.Ceiling(area.Left); x < area.Right; x++)
        {
            var gray = pixels[(y * size.Width + x) * 4];
            var value = Math.Max(0, invert ? 215 - gray : gray - 40);
            weight += value;
            xSum += (x + .5) * value;
            ySum += (y + .5) * value;
        }
        weight.Should().BeGreaterThan(0);
        return new Point(xSum / weight, ySum / weight);
    }

    private static Point RingCenter(ImageViewTestSurface surface)
    {
        var groups = RenderedMinutiaComponents.Create(ColoredPixels(surface));
        groups.Should().HaveCount(2);
        var ring = groups.MaxBy(group => group.Count)!;
        return new Point(
            (ring.Min(p => p.X) + ring.Max(p => p.X)) / 2,
            (ring.Min(p => p.Y) + ring.Max(p => p.Y)) / 2
        );
    }

    private static IEnumerable<Point> ColoredPixels(ImageViewTestSurface surface)
    {
        var (size, pixels) = surface.RenderPixels();
        var origin = surface.Image.TranslatePoint(default, surface.Window)!.Value;
        var area = new Rect(origin, surface.Image.Bounds.Size).Deflate(2);
        return Enumerable
            .Range(0, size.Width * size.Height)
            .Where(i =>
                Math.Max(pixels[i * 4], Math.Max(pixels[i * 4 + 1], pixels[i * 4 + 2]))
                    - Math.Min(pixels[i * 4], Math.Min(pixels[i * 4 + 1], pixels[i * 4 + 2]))
                > 20
            )
            .Select(i => new Point(i % size.Width, i / size.Width))
            .Where(area.Contains);
    }
}
