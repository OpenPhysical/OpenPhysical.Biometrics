// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal static class WorkspaceReviewImages
{
    internal static bool Save(Window window, Size size, string label)
    {
        window.UpdateLayout();
        return Save((Control)window, size, label);
    }

    internal static bool Save(Control control, Size size, string label)
    {
        var folder = Environment.GetEnvironmentVariable("OPENPHYSICAL_REVIEW_IMAGES");
        if (string.IsNullOrWhiteSpace(folder))
            return false;
        using var rendered = new RenderTargetBitmap(
            new PixelSize((int)size.Width, (int)size.Height)
        );
        rendered.Render(control);
        rendered.Save(Path.Combine(folder, $"studio-{size.Width}x{size.Height}-{label}.png"));
        return true;
    }
}
