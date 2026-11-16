// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Buffers.Binary;
using CSharpFunctionalExtensions;

// The decoder can recover truncated streams; saved portrait review requires complete input.
internal static class Jpeg2000Input
{
    internal static UnitResult<FacialError> Check(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length is < 1 or > PortraitImage.MaximumEncodedBytes)
            return FacialErrors.EncodedImageSize();
        if (encoded.StartsWith(new byte[] { 255, 79 }))
            return Codestream(encoded);
        if (!encoded.StartsWith(new byte[] { 0, 0, 0, 12, 106, 80, 32, 32, 13, 10, 135, 10 }))
            return FacialErrors.UnsupportedImage();
        return Boxes(encoded);
    }

    private static UnitResult<FacialError> Boxes(ReadOnlySpan<byte> encoded)
    {
        var streams = 0;
        var boxes = 0;
        for (var offset = 0; offset < encoded.Length; )
        {
            var remaining = encoded[offset..];
            if (++boxes > 4096 || remaining.Length < 8)
                return FacialErrors.UnsupportedImage();
            var size = BoxSize(remaining);
            if (size.IsFailure)
                return size.Error;
            var (length, header) = size.Value;
            if (remaining.Slice(4, 4).SequenceEqual("jp2c"u8))
            {
                if (++streams != 1)
                    return FacialErrors.UnsupportedImage();
                var valid = Codestream(remaining.Slice(header, length - header));
                if (valid.IsFailure)
                    return valid;
            }
            offset += length;
        }
        return streams == 1 ? UnitResult.Success<FacialError>() : FacialErrors.UnsupportedImage();
    }

    private static Result<(int Length, int Header), FacialError> BoxSize(ReadOnlySpan<byte> data)
    {
        ulong length = BinaryPrimitives.ReadUInt32BigEndian(data);
        var header = 8;
        if (length == 1)
        {
            if (data.Length < 16)
                return FacialErrors.UnsupportedImage();
            length = BinaryPrimitives.ReadUInt64BigEndian(data[8..]);
            header = 16;
        }
        else if (length == 0)
            length = (ulong)data.Length;
        return length < (ulong)header || length > (ulong)data.Length
            ? FacialErrors.UnsupportedImage()
            : ((int)length, header);
    }

    private static UnitResult<FacialError> Codestream(ReadOnlySpan<byte> encoded) =>
        encoded.Length >= 4
        && encoded.StartsWith(new byte[] { 255, 79 })
        && encoded.EndsWith(new byte[] { 255, 217 })
            ? UnitResult.Success<FacialError>()
            : FacialErrors.UnsupportedImage();
}
