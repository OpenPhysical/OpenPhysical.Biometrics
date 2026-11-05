// SPDX-License-Identifier: GPL-2.0-or-later

using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Capture;

/// <summary>Bounded dimensions for tightly packed grayscale frames.</summary>
public sealed record CaptureGeometry
{
    private CaptureGeometry(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>Gets the horizontal pixel count.</summary>
    public int Width { get; }

    /// <summary>Gets the vertical pixel count.</summary>
    public int Height { get; }

    /// <summary>Gets the total tightly packed pixel count.</summary>
    public int PixelCount => Width * Height;

    /// <summary>Validates dimensions before any image allocation.</summary>
    /// <param name="width">Horizontal pixels.</param>
    /// <param name="height">Vertical pixels.</param>
    /// <returns>Dimensions limited to sixteen megapixels, or a geometry failure.</returns>
    public static Result<CaptureGeometry, CaptureError> Create(int width, int height) =>
        width > 0 && height > 0 && (long)width * height <= 16 * 1024 * 1024
            ? new CaptureGeometry(width, height)
            : new CaptureError(
                CaptureErrorCodes.UnsupportedGeometry,
                "Frame dimensions exceed the supported bounds."
            );
}
