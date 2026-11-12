// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal static class DesktopBitmap
{
    internal static Result<WriteableBitmap, DesktopError> Create(
        ImmutableArray<byte> pixels,
        int width,
        int height,
        bool invert = false
    ) =>
        DesktopEdge.Try(
            () => Copy(pixels, width, height, invert),
            "ImageDisplay",
            "Cannot display image."
        );

    private static WriteableBitmap Copy(
        ImmutableArray<byte> pixels,
        int width,
        int height,
        bool invert
    )
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque
        );
        using var framebuffer = bitmap.Lock();
        var row = new byte[width * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var original = pixels[y * width + x];
                var value = invert ? (byte)(255 - original) : original;
                row[x * 4] = value;
                row[x * 4 + 1] = value;
                row[x * 4 + 2] = value;
                row[x * 4 + 3] = 255;
            }
            Marshal.Copy(row, 0, framebuffer.Address + y * framebuffer.RowBytes, row.Length);
        }
        return bitmap;
    }
}
