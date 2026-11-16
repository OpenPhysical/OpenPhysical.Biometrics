// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Imaging;

// Facial error identity stays stable while the shared codec owns EXIF and color handling.
internal static class PortraitDecoder
{
    internal static Result<PortraitPixels, FacialError> Decode(ReadOnlySpan<byte> encoded)
    {
        var bytes = encoded.ToArray();
        try
        {
            return RgbImageDecoder.LoadSrgbOpaque(bytes).MapError(Map).Bind(Copy);
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    private static Result<PortraitPixels, FacialError> Copy(RgbImage image)
    {
        using (image)
            return image
                .CopyRgbBytes()
                .MapError(Map)
                .Map(rgb => Own(image.Width, image.Height, rgb));
    }

    private static PortraitPixels Own(int width, int height, byte[] pixels)
    {
        var owned = new PortraitPixels(width, height, ImmutableArray.Create(pixels));
        Array.Clear(pixels);
        return owned;
    }

    private static FacialError Map(ImagingError error) =>
        error.Code switch
        {
            ImagingErrorCodes.InvalidDimensions => FacialErrors.ImageDimensionsExceeded(),
            ImagingErrorCodes.TransparencyUnsupported => FacialErrors.ImageNotOpaque(),
            ImagingErrorCodes.FormatUnsupported => FacialErrors.UnsupportedImage(),
            ImagingErrorCodes.DecodeFailed => FacialErrors.ImageDecodeFailed(),
            ImagingErrorCodes.NativeRuntimeUnavailable => new FacialError(
                FacialErrorCodes.NativeRuntimeUnavailable,
                "The native image runtime is unavailable."
            ),
            _ => new FacialError(FacialErrorCodes.ImageProcessingFailed, error.Message),
        };
}
