// SPDX-License-Identifier: GPL-2.0-or-later
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using AwesomeAssertions;
using Material.Styles.Controls;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class MaterialSurfaceTests
{
    [AvaloniaTest]
    public async Task ImageCardShadowExtendsPastTheStraightEdgeRatherThanOnlyTheCorners()
    {
        var host = DesktopServices.CreateHost();
        var view = new EnrollmentView
        {
            DataContext = host.Services.GetRequiredService<WorkspaceViewModel>(),
        };
        var window = new Window
        {
            Content = view,
            Width = 1280,
            Height = 880,
        };
        window.Show();
        try
        {
            var card = view.GetVisualDescendants().OfType<Card>().Single();
            var origin = card.TranslatePoint(default, window)!.Value;
            using var rendered = new RenderTargetBitmap(new PixelSize(1280, 880));
            rendered.Render(window);
            BlueAt(rendered, (int)origin.X - 1, (int)(origin.Y + card.Bounds.Height / 2))
                .Should()
                .BeLessThan(243);
        }
        finally
        {
            window.Close();
            await ((IAsyncDisposable)host).DisposeAsync();
        }
    }

    private static byte BlueAt(Bitmap bitmap, int x, int y)
    {
        var bytes = new byte[4];
        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(x, y, 1, 1), pinned.AddrOfPinnedObject(), 4, 4);
            return bytes[0];
        }
        finally
        {
            pinned.Free();
        }
    }

    [AvaloniaTest]
    public void BodyControlsUseOneReadableSizeAndNormalWeight()
    {
        var text = new TextBlock { Text = "Capture status", Classes = { "caption" } };
        var label = new TextBlock { Text = "First finger", Classes = { "label" } };
        var button = new Button { Content = "Save", Classes = { "compact" } };
        var panel = new StackPanel { Children = { text, label, button } };
        var window = new Window
        {
            Content = panel,
            Width = 400,
            Height = 260,
        };
        window.Show();
        try
        {
            text.FontSize.Should().Be(16);
            label.FontSize.Should().Be(16);
            button.FontSize.Should().Be(16);
            text.FontWeight.Should().Be(FontWeight.Normal);
            label.FontWeight.Should().Be(FontWeight.Normal);
            button.FontWeight.Should().Be(FontWeight.Normal);
        }
        finally
        {
            window.Close();
        }
    }
}
