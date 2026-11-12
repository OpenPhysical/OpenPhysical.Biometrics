// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;

[TestFixture]
internal sealed class DisplayInversionTests
{
    [AvaloniaTest]
    public void InvertAndSceneReplacementChangeOnlyDisplayedSamples()
    {
        using var first = Scene([0, 80, 160, 255]);
        using var next = Scene([32, 64, 128, 223]);
        using var surface = new ImageViewTestSurface(first);
        surface.View.ShowFeatures = surface.View.ShowArea = false;
        Samples(surface).Should().Equal(Gray(0), Gray(80), Gray(160), Gray(255));
        surface.View.Invert = true;
        Samples(surface).Should().Equal(Gray(255), Gray(175), Gray(95), Gray(0));
        surface.View.Scene = next;
        Samples(surface).Should().Equal(Gray(223), Gray(191), Gray(127), Gray(32));
        surface.View.Invert = false;
        Samples(surface).Should().Equal(Gray(32), Gray(64), Gray(128), Gray(223));
        surface.View.Invert = true;
        Samples(surface).Should().Equal(Gray(223), Gray(191), Gray(127), Gray(32));
        first.GrayPixels.Should().Equal(0, 80, 160, 255);
        next.GrayPixels.Should().Equal(32, 64, 128, 223);
    }

    [AvaloniaTest]
    public void EmptyViewportUsesPureWhiteOrBlackWithoutAStaleImage()
    {
        using var scene = Scene([0, 80, 160, 255]);
        using var surface = new ImageViewTestSurface(scene);
        surface.View.Scene = Maybe<FingerprintScene>.None;
        Center(surface).Should().Be(Colors.White);
        surface.View.Invert = true;
        Center(surface).Should().Be(Colors.Black);
    }

    private static FingerprintScene Scene(ImmutableArray<byte> pixels)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(pixels.Length, 1),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque
        );
        using var frame = bitmap.Lock();
        var bytes = pixels.SelectMany(value => new byte[] { value, value, value, 255 }).ToArray();
        Marshal.Copy(bytes, 0, frame.Address, bytes.Length);
        return new FingerprintScene(pixels.Length, 1, [], Maybe.From(bitmap))
        {
            GrayPixels = pixels,
        };
    }

    private static Color[] Samples(ImageViewTestSurface surface)
    {
        var (size, pixels) = surface.RenderPixels();
        var origin = surface.Image.TranslatePoint(default, surface.Window)!.Value;
        var count = surface.View.Scene.Value.Width;
        var scale = Math.Min(surface.Image.Bounds.Width / count, surface.Image.Bounds.Height);
        return Enumerable
            .Range(0, count)
            .Select(index =>
                At(
                    pixels,
                    size.Width,
                    (int)(
                        origin.X
                        + (surface.Image.Bounds.Width - count * scale) / 2
                        + (index + .5) * scale
                    ),
                    (int)(origin.Y + surface.Image.Bounds.Height / 2)
                )
            )
            .ToArray();
    }

    private static Color Center(ImageViewTestSurface surface)
    {
        var (size, pixels) = surface.RenderPixels();
        var origin = surface.Image.TranslatePoint(default, surface.Window)!.Value;
        return At(
            pixels,
            size.Width,
            (int)(origin.X + surface.Image.Bounds.Width / 2),
            (int)(origin.Y + surface.Image.Bounds.Height / 2)
        );
    }

    private static Color At(byte[] pixels, int width, int x, int y)
    {
        var offset = (y * width + x) * 4;
        return Color.FromArgb(
            pixels[offset + 3],
            pixels[offset + 2],
            pixels[offset + 1],
            pixels[offset]
        );
    }

    private static Color Gray(byte value) => Color.FromRgb(value, value, value);
}
