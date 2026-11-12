// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using System.Buffers.Binary;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;

internal static class CaptureProcessingDigest
{
    internal static string Pixels(ReadOnlySpan<byte> bytes)
    {
        var digest = new Sha256Digest();
        digest.BlockUpdate(bytes);
        return Finish(digest);
    }

    internal static string Fields(params string[] values)
    {
        var digest = new Sha256Digest();
        Span<byte> length = stackalloc byte[4];
        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
            digest.BlockUpdate(length);
            digest.BlockUpdate(bytes);
        }
        return Finish(digest);
    }

    internal static string Mask(
        int width,
        int height,
        ReadOnlySpan<byte> retained,
        ReadOnlySpan<byte> inner
    )
    {
        var digest = new Sha256Digest();
        Span<byte> dimensions = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(dimensions, width);
        BinaryPrimitives.WriteInt32LittleEndian(dimensions[4..], height);
        digest.BlockUpdate(dimensions);
        digest.BlockUpdate(retained);
        digest.BlockUpdate(inner);
        return Finish(digest);
    }

    private static string Finish(Sha256Digest digest)
    {
        Span<byte> bytes = stackalloc byte[32];
        _ = digest.DoFinal(bytes);
        return Convert.ToHexStringLower(bytes);
    }
}
