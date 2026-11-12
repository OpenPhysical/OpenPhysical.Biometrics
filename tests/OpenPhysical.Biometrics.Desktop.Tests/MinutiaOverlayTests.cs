// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class MinutiaOverlayTests
{
    [AvaloniaTest]
    [TestCase(0, 1, 0)]
    [TestCase(45, 0, -1)]
    [TestCase(90, -1, 0)]
    [TestCase(135, 0, 1)]
    public void RenderedAnsiDirectionsUseCounterclockwiseAnglesAndTopOrigin(
        byte angle,
        int x,
        int y
    )
    {
        using var scene = new FingerprintScene(101, 101, [new(1, 50, 50, angle, 100)], Maybe.None);
        using var surface = new ImageViewTestSurface(scene);
        var image = surface.Image;
        var origin = image.TranslatePoint(default, surface.Window)!.Value;
        var center = origin + new Vector(image.Bounds.Width / 2, image.Bounds.Height / 2);
        var tip = center + new Vector(x * 10, y * 10);
        var opposite = center - new Vector(x * 10, y * 10);
        var pixels = surface.FeaturePixels();
        pixels
            .Should()
            .Contain(point => Math.Pow(point.X - tip.X, 2) + Math.Pow(point.Y - tip.Y, 2) < 4);
        pixels
            .Should()
            .NotContain(point =>
                Math.Pow(point.X - opposite.X, 2) + Math.Pow(point.Y - opposite.Y, 2) < 4
            );
        surface.View.ShowFeatures = false;
        surface.FeaturePixels().Should().BeEmpty();
    }
}
