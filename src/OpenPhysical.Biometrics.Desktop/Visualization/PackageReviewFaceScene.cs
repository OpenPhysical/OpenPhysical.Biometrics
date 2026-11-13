// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Imaging;

internal static class PackageReviewFaceScene
{
    internal static Result<FacePreviewScene, DesktopError> Create(FaceImage face)
    {
        var encoded = face.ImageData.ToArray();
        try
        {
            var decoded = Decode(face.ImageInformation.ImageDataType, encoded);
            if (decoded.IsFailure)
                return decoded.Error;
            using var image = decoded.Value;
            if (
                image.Width != face.ImageInformation.Width
                || image.Height != face.ImageInformation.Height
            )
                return new DesktopError(
                    "Storage.Record",
                    "The portrait pixels do not match their record dimensions."
                );
            var rgb = image
                .CopyRgbBytes()
                .MapError(error => new DesktopError("Storage.Record", error.Message));
            if (rgb.IsFailure)
                return rgb.Error;
            try
            {
                return FacePreviewScene.Create(rgb.Value, image.Width, image.Height);
            }
            finally
            {
                Array.Clear(rgb.Value);
            }
        }
        finally
        {
            Array.Clear(encoded);
        }
    }

    private static Result<RgbImage, DesktopError> Decode(byte type, byte[] encoded) =>
        type switch
        {
            0 => RgbImageDecoder
                .LoadSrgbOpaque(encoded)
                .MapError(error => new DesktopError("Storage.Record", error.Message)),
            1 => PortraitEncoding
                .DecodeJpeg2000(encoded)
                .MapError(error => new DesktopError(error.Code, error.Message)),
            _ => new DesktopError("Storage.Record", "The portrait image encoding is unsupported."),
        };
}
