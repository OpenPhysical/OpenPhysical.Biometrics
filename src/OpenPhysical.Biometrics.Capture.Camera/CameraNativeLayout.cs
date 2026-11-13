// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using System.Buffers.Binary;

/// <summary>Non-image metadata for diagnosing an unsupported native RGB packet.</summary>
/// <param name="PacketBytes">Packet length without retaining its content.</param>
/// <param name="HeaderBytes">Declared bitmap header size.</param>
/// <param name="Width">Declared wrapper width.</param>
/// <param name="Height">Declared wrapper height.</param>
/// <param name="PixelBits">Declared pixel bit depth.</param>
/// <param name="RowBytes">Payload-derived row length, or zero when unavailable.</param>
/// <param name="RowRemainder">Payload bytes remaining after complete declared rows.</param>
/// <param name="Format">Advertised native format.</param>
public sealed record CameraNativeLayout(
    int PacketBytes,
    int HeaderBytes,
    int Width,
    int Height,
    int PixelBits,
    int RowBytes,
    int RowRemainder,
    string Format
)
{
    internal static CameraNativeLayout Describe(ReadOnlySpan<byte> bytes, CameraMode mode)
    {
        if (bytes.Length < 54)
            return new(bytes.Length, 0, 0, 0, 0, 0, 0, mode.PixelFormat);
        var offset = BinaryPrimitives.ReadInt32LittleEndian(bytes[10..]);
        var height = BinaryPrimitives.ReadInt32LittleEndian(bytes[22..]);
        var usable = offset >= 54 && offset <= bytes.Length && height > 0;
        return new(
            bytes.Length,
            BinaryPrimitives.ReadInt32LittleEndian(bytes[14..]),
            BinaryPrimitives.ReadInt32LittleEndian(bytes[18..]),
            height,
            BinaryPrimitives.ReadInt16LittleEndian(bytes[28..]),
            usable ? (bytes.Length - offset) / height : 0,
            usable ? (bytes.Length - offset) % height : 0,
            mode.PixelFormat
        );
    }
}
