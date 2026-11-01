// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Imaging;

internal static class BaselineTiffEncoder
{
    private const ushort Entries = 14;
    private const int BitsOffset = 8 + 2 + Entries * 12 + 4;
    private const int XResolutionOffset = BitsOffset + 6;
    private const int YResolutionOffset = XResolutionOffset + 8;
    private const int PixelOffset = YResolutionOffset + 8;

    internal static UnitResult<ImagingError> Encode(RgbImage image, Stream stream) =>
        image.IsDisposed
            ? UnitResult.Failure(ImagingErrors.Disposed())
            : ImagingGuard.TryUnit(() => Write(image, stream), ImagingErrorCodes.IoFailure);

    private static UnitResult<ImagingError> Write(RgbImage image, Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        WritePixels(WriteHeader(writer, image), image);
        return UnitResult.Success<ImagingError>();
    }

    private static BinaryWriter WriteHeader(BinaryWriter writer, RgbImage image)
    {
        writer.Write(new byte[] { (byte)'I', (byte)'I', 42, 0 });
        writer.Write(8u);
        writer.Write(Entries);
        Entry(writer, 256, 4, 1, (uint)image.Width);
        Entry(writer, 257, 4, 1, (uint)image.Height);
        Entry(writer, 258, 3, 3, BitsOffset);
        Entry(writer, 259, 3, 1, 1);
        Entry(writer, 262, 3, 1, 2);
        Entry(writer, 273, 4, 1, PixelOffset);
        Entry(writer, 274, 3, 1, 1);
        Entry(writer, 277, 3, 1, 3);
        Entry(writer, 278, 4, 1, (uint)image.Height);
        Entry(writer, 279, 4, 1, (uint)(image.Width * image.Height * 3));
        Entry(writer, 282, 5, 1, XResolutionOffset);
        Entry(writer, 283, 5, 1, YResolutionOffset);
        Entry(writer, 284, 3, 1, 1);
        Entry(writer, 296, 3, 1, 2);
        writer.Write(0u);
        writer.Write((ushort)8);
        writer.Write((ushort)8);
        writer.Write((ushort)8);
        // Pixel-only images use a documented 72 ppi display resolution.
        writer.Write(72u);
        writer.Write(1u);
        writer.Write(72u);
        writer.Write(1u);
        return writer;
    }

    private static BinaryWriter WritePixels(BinaryWriter writer, RgbImage image)
    {
        foreach (var pixel in image.Pixels)
        {
            writer.Write(pixel.R);
            writer.Write(pixel.G);
            writer.Write(pixel.B);
        }
        return writer;
    }

    private static BinaryWriter Entry(
        BinaryWriter writer,
        ushort tag,
        ushort type,
        uint count,
        uint value
    )
    {
        writer.Write(tag);
        writer.Write(type);
        writer.Write(count);
        writer.Write(value);
        return writer;
    }
}
