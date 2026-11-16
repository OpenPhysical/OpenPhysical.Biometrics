// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Imaging;

using CSharpFunctionalExtensions;

internal static class ImageOrientation
{
    internal static Result<RgbImage, ImagingError> Apply(
        RgbaPixel[] pixels,
        int width,
        int height,
        int origin,
        bool opaque
    )
    {
        if (opaque && pixels.Any(pixel => pixel.A != 255))
        {
            Array.Clear(pixels);
            return new ImagingError(
                ImagingErrorCodes.TransparencyUnsupported,
                "The image must contain opaque pixels."
            );
        }
        if (origin == 1)
            return RgbImage.Adopt(pixels, width, height);
        var rotated = origin >= 5;
        var outputWidth = rotated ? height : width;
        var outputHeight = rotated ? width : height;
        var output = new RgbaPixel[pixels.Length];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var (dx, dy) = Destination(origin, width, height, x, y);
            output[dy * outputWidth + dx] = pixels[y * width + x];
        }
        Array.Clear(pixels);
        return RgbImage.Adopt(output, outputWidth, outputHeight);
    }

    private static (int X, int Y) Destination(int origin, int width, int height, int x, int y) =>
        origin switch
        {
            2 => (width - 1 - x, y),
            3 => (width - 1 - x, height - 1 - y),
            4 => (x, height - 1 - y),
            5 => (y, x),
            6 => (height - 1 - y, x),
            7 => (height - 1 - y, width - 1 - x),
            8 => (y, width - 1 - x),
            _ => (x, y),
        };
}
