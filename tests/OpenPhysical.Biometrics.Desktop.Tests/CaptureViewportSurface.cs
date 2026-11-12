// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal sealed class CaptureViewportSurface(
    IHost host,
    WorkspaceViewModel workspace,
    ImageViewTestSurface surface,
    CapturedFingerprint source
) : IAsyncDisposable
{
    internal CaptureViewModel Capture => workspace.Capture;
    internal ImageViewTestSurface Surface => surface;
    internal CapturedFingerprint Source => source;
    internal Rect ImageArea =>
        new(
            surface.Image.TranslatePoint(default, surface.Window)!.Value,
            surface.Image.Bounds.Size
        );

    internal static async Task<CaptureViewportSurface> Create(Size size)
    {
        var host = DesktopServices.CreateHost();
        var workspace = host.Services.GetRequiredService<WorkspaceViewModel>();
        var source = HullStorageFixtures.Capture();
        (await workspace.Capture.ReviewAcceptedAsync(source)).Should().Succeed();
        var surface = new ImageViewTestSurface(workspace.Capture.Scene.Value, size);
        surface.View.ShowFeatures = false;
        surface.View.Bind(
            BiometricImageView.SceneProperty,
            new Binding(nameof(CaptureViewModel.Scene)) { Source = workspace.Capture }
        );
        return new(host, workspace, surface, source);
    }

    internal Rect Outline()
    {
        var (size, pixels) = surface.RenderPixels();
        var image = ImageArea.Deflate(2);
        var points = Enumerable
            .Range(0, size.Width * size.Height)
            .Where(i =>
                pixels[i * 4 + 1] > 40
                && pixels[i * 4 + 1] - Math.Min(pixels[i * 4], pixels[i * 4 + 2]) > 4
                && Math.Abs(Math.Max(pixels[i * 4], pixels[i * 4 + 2]) - pixels[i * 4 + 1]) < 8
            )
            .Select(i => new Point(i % size.Width, i / size.Width))
            .Where(image.Contains)
            .ToArray();
        points.Should().NotBeEmpty();
        return new Rect(
            new Point(points.Min(p => p.X), points.Min(p => p.Y)),
            new Point(points.Max(p => p.X), points.Max(p => p.Y))
        );
    }

    internal bool Pan()
    {
        surface.Image.Focus();
        surface.Window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        surface.Window.KeyReleaseQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        surface.Dispose();
        await workspace.Capture.StopAsync();
        await workspace.Inspection.StopAsync();
        await ((IAsyncDisposable)host).DisposeAsync();
    }
}
