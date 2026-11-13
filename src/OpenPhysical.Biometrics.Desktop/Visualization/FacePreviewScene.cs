// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CSharpFunctionalExtensions;

/// <summary>An owned display copy of source or prepared facial pixels.</summary>
public sealed class FacePreviewScene : IDisposable
{
    private FacePreviewScene(WriteableBitmap image, int width, int height)
    {
        Image = image;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the display bitmap.</summary>
    public WriteableBitmap Image { get; }

    /// <summary>Gets source pixel width.</summary>
    public int Width { get; }

    /// <summary>Gets source pixel height.</summary>
    public int Height { get; }

    internal static Result<FacePreviewScene, DesktopError> Create(
        ReadOnlySpan<byte> rgb,
        int width,
        int height
    )
    {
        var copy = rgb.ToArray();
        try
        {
            return DesktopEdge.Try(
                () =>
                {
                    var bitmap = new WriteableBitmap(
                        new PixelSize(width, height),
                        new Vector(96, 96),
                        PixelFormat.Rgba8888,
                        AlphaFormat.Opaque
                    );
                    CopyRows(bitmap, copy, width, height);
                    return new FacePreviewScene(bitmap, width, height);
                },
                "Face.Preview",
                "The camera image could not be displayed."
            );
        }
        finally
        {
            Array.Clear(copy);
        }
    }

    private static bool CopyRows(WriteableBitmap bitmap, byte[] rgb, int width, int height)
    {
        using var locked = bitmap.Lock();
        var row = new byte[width * 4];
        try
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var source = (y * width + x) * 3;
                    row[x * 4] = rgb[source];
                    row[x * 4 + 1] = rgb[source + 1];
                    row[x * 4 + 2] = rgb[source + 2];
                    row[x * 4 + 3] = 255;
                }
                System.Runtime.InteropServices.Marshal.Copy(
                    row,
                    0,
                    locked.Address + y * locked.RowBytes,
                    row.Length
                );
            }
            return true;
        }
        finally
        {
            Array.Clear(row);
        }
    }

    /// <summary>Releases the display pixels.</summary>
    public void Dispose() => Image.Dispose();
}
