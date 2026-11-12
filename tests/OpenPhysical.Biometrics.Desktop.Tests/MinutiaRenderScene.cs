// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;

internal static class MinutiaRenderScene
{
    internal static FingerprintScene PublicImage(byte[] pixels)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(800, 750),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque
        );
        using var frame = bitmap.Lock();
        var bgra = new byte[pixels.Length * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            bgra[i * 4] = bgra[i * 4 + 1] = bgra[i * 4 + 2] = pixels[i];
            bgra[i * 4 + 3] = 255;
        }
        Marshal.Copy(bgra, 0, frame.Address, bgra.Length);
        // Synthetic display markers on the public SD04 image, not extraction results.
        var points = Enumerable
            .Range(0, 8)
            .Select(i => new FingerprintMinutia(
                (byte)(i < 4 ? 1 : 2),
                (ushort)(250 + i % 4 * 100),
                (ushort)(i < 4 ? 250 : 450),
                (byte)(i % 4 * 45),
                80
            ))
            .ToImmutableArray();
        return new FingerprintScene(800, 750, points, Maybe.From(bitmap))
        {
            GrayPixels = pixels.ToImmutableArray(),
        };
    }
}
