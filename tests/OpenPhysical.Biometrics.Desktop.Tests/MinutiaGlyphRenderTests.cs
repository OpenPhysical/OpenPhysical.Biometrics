// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia;
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;

[TestFixture]
internal sealed class MinutiaGlyphRenderTests
{
    [AvaloniaTest]
    [TestCase(1, false, 2)]
    [TestCase(1, true, 2)]
    [TestCase(2, false, 3)]
    [TestCase(2, true, 3)]
    public void HollowCircleAndDetachedDirectionTicksStayReadableWhenZooming(
        byte type,
        bool invert,
        int shapes
    )
    {
        using var scene = new FingerprintScene(101, 101, [new(type, 50, 50, 0, 100)], Maybe.None);
        using var surface = new ImageViewTestSurface(scene);
        surface.View.Invert = invert;
        var before = CheckGlyph(surface, shapes);
        for (var i = 0; i < 3; i++)
            surface.Press("Zoom in");
        var after = CheckGlyph(surface, shapes);
        Math.Abs(after.Width - before.Width).Should().BeLessThanOrEqualTo(2);
        Math.Abs(after.Height - before.Height).Should().BeLessThanOrEqualTo(2);
    }

    [AvaloniaTest]
    [TestCase(false)]
    [TestCase(true)]
    public void BackgroundColoredHaloSeparatesMarkersFromPublicRidgesWithoutChangingPixels(
        bool invert
    )
    {
        var source = ProcessingFrameFixtures.PublicFinger();
        using var scene = MinutiaRenderScene.PublicImage(source);
        using var surface = new ImageViewTestSurface(scene, new Size(1100, 760));
        surface.View.Invert = invert;
        surface.View.ShowFeatures = false;
        surface.Window.UpdateLayout();
        var before = surface.RenderPixels().Bgra;
        surface.View.ShowFeatures = true;
        var after = surface.RenderPixels().Bgra;
        _ = CheckUncoveredRidges(surface, before, after);
        Enumerable
            .Range(0, after.Length / 4)
            .Should()
            .Contain(i => HaloChanged(before, after, i, invert));
        scene.GrayPixels.Should().Equal(source);
        var name = invert ? "inverted" : "normal";
        WorkspaceReviewImages.Save(surface.Window, new Size(1100, 760), $"minutiae-{name}-fit");
        for (var i = 0; i < 3; i++)
            surface.Press("Zoom in");
        WorkspaceReviewImages.Save(surface.Window, new Size(1100, 760), $"minutiae-{name}-zoom");
    }

    private static bool CheckUncoveredRidges(
        ImageViewTestSurface surface,
        byte[] before,
        byte[] after
    )
    {
        var image = surface.Image;
        var scene = surface.View.Scene.Value;
        var point = scene.Minutiae.First(value => value.Type == 2 && value.Angle == 0);
        var scale = Math.Min(image.Bounds.Width / scene.Width, image.Bounds.Height / scene.Height);
        var origin = image.TranslatePoint(default, surface.Window)!.Value;
        var x = origin.X + (image.Bounds.Width - scene.Width * scale) / 2 + (point.X + .5) * scale;
        var y =
            origin.Y + (image.Bounds.Height - scene.Height * scale) / 2 + (point.Y + .5) * scale;
        var width = (int)surface.Window.Bounds.Width;
        foreach (var offset in new[] { 0, 10, 11 })
        {
            var index = ((int)y * width + (int)(x + offset)) * 4;
            for (var channel = 0; channel < 3; channel++)
                Math.Abs(after[index + channel] - before[index + channel])
                    .Should()
                    .BeLessThanOrEqualTo(
                        8,
                        "the circle center and valley between detached ticks remain visible"
                    );
        }
        return true;
    }

    private static bool HaloChanged(byte[] before, byte[] after, int index, bool invert)
    {
        var i = index * 4;
        var gray =
            Math.Abs(after[i] - after[i + 1]) < 4 && Math.Abs(after[i + 1] - after[i + 2]) < 4;
        return gray && (invert ? after[i] < before[i] - 15 : after[i] > before[i] + 15);
    }

    private static Rect CheckGlyph(ImageViewTestSurface surface, int shapes)
    {
        var image = surface.Image;
        var origin = image.TranslatePoint(default, surface.Window)!.Value;
        var center = origin + new Vector(image.Bounds.Width / 2, image.Bounds.Height / 2);
        var pixels = surface.FeaturePixels();
        pixels.Should().NotBeEmpty();
        pixels
            .Should()
            .NotContain(p => Math.Pow(p.X - center.X, 2) + Math.Pow(p.Y - center.Y, 2) < 4);
        RenderedMinutiaComponents.Create(pixels).Should().HaveCount(shapes);
        var bounds = new Rect(
            new Point(pixels.Min(p => p.X), pixels.Min(p => p.Y)),
            new Point(pixels.Max(p => p.X), pixels.Max(p => p.Y))
        );
        bounds.Width.Should().BeInRange(15, 23);
        bounds.Height.Should().BeInRange(6, 13);
        return bounds;
    }
}
