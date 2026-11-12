// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class FingerprintViewportTests
{
    [AvaloniaTest]
    public void VisibleZoomButtonsChangeFeatureScaleAndFitRestoresIt()
    {
        using var scene = new FingerprintScene(
            100,
            100,
            [new(1, 30, 50, 0, 100), new(1, 70, 50, 0, 100)],
            Maybe.None
        );
        using var surface = new ImageViewTestSurface(scene);
        var initial = Extent(surface);
        surface.Press("Zoom in");
        Extent(surface).Should().BeGreaterThan(initial);
        surface.Press("Zoom out");
        Extent(surface).Should().BeApproximately(initial, 1);
        surface.Press("Zoom in");
        surface.Press("Fit image");
        Extent(surface).Should().BeApproximately(initial, 1);
    }

    private static double Extent(ImageViewTestSurface surface)
    {
        var points = surface.FeaturePixels();
        points.Should().NotBeEmpty();
        return points.Max(point => point.X) - points.Min(point => point.X);
    }

    [AvaloniaTest]
    public void KeyboardPansAndHomeRestoresTheImage()
    {
        using var scene = new FingerprintScene(100, 100, [new(1, 50, 50, 0, 100)], Maybe.None);
        using var surface = new ImageViewTestSurface(scene);
        surface.Image.Focus();
        var initial = surface.FeaturePixels().Min(point => point.X);
        surface.Window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        surface.Window.KeyReleaseQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        surface.FeaturePixels().Min(point => point.X).Should().BeGreaterThan(initial);
        surface.Window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        surface.Window.KeyReleaseQwerty(PhysicalKey.Home, RawInputModifiers.None);
        surface.FeaturePixels().Min(point => point.X).Should().BeApproximately(initial, 1);
    }
}
