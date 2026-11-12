// SPDX-License-Identifier: GPL-2.0-or-later
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal sealed class ImageViewTestSurface : IDisposable
{
    internal BiometricImageView View { get; } = new();
    internal Window Window { get; }

    internal ImageViewTestSurface(FingerprintScene scene)
        : this(scene, new Size(420, 360)) { }

    internal ImageViewTestSurface(FingerprintScene scene, Size size)
    {
        View.Scene = scene;
        Window = new Window
        {
            Content = View,
            Width = size.Width,
            Height = size.Height,
        };
        Window.Show();
    }

    internal Control Image =>
        View.GetVisualDescendants()
            .OfType<Control>()
            .Single(control =>
                AutomationProperties.GetName(control)?.StartsWith("Fingerprint image,") == true
            );

    internal Button Press(string name)
    {
        var button = View.GetVisualDescendants()
            .OfType<Button>()
            .Single(item => AutomationProperties.GetName(item) == name);
        button.Command!.Execute(button.CommandParameter);
        return button;
    }

    internal Point[] FeaturePixels()
    {
        var (size, pixels) = RenderPixels();
        var origin = Image.TranslatePoint(default, Window)!.Value;
        var area = new Rect(origin, Image.Bounds.Size).Deflate(2);
        return Enumerable
            .Range(0, size.Width * size.Height)
            .Where(index =>
                pixels[index * 4 + 1] > 40
                && pixels[index * 4 + 1] - pixels[index * 4] > 20
                && pixels[index * 4 + 1] - pixels[index * 4 + 2] > 20
            )
            .Select(index => new Point(index % size.Width, index / size.Width))
            .Where(area.Contains)
            .ToArray();
    }

    internal (PixelSize Size, byte[] Bgra) RenderPixels()
    {
        var size = new PixelSize((int)Window.Bounds.Width, (int)Window.Bounds.Height);
        using var bitmap = new RenderTargetBitmap(size);
        bitmap.Render(Window);
        var pixels = new byte[size.Width * size.Height * 4];
        var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(
                new PixelRect(size),
                pinned.AddrOfPinnedObject(),
                pixels.Length,
                size.Width * 4
            );
        }
        finally
        {
            pinned.Free();
        }
        return (size, pixels);
    }

    public void Dispose() => Window.Close();
}
