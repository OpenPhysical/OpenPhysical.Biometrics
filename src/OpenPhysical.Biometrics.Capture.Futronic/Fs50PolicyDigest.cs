// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Digests;

internal static class Fs50PolicyDigest
{
    internal static bool IsDigest(string value) =>
        value is not null
        && value.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static string Create<T>(string version, T options)
    {
        var hash = new Sha256Digest();
        hash.BlockUpdate(
            Encoding.UTF8.GetBytes(version + "\n" + JsonSerializer.Serialize(options))
        );
        Span<byte> digest = stackalloc byte[32];
        _ = hash.DoFinal(digest);
        return Convert.ToHexStringLower(digest);
    }

    internal static string Pixels(ReadOnlySpan<byte> pixels)
    {
        var hash = new Sha256Digest();
        hash.BlockUpdate(pixels);
        Span<byte> digest = stackalloc byte[32];
        _ = hash.DoFinal(digest);
        return Convert.ToHexStringLower(digest);
    }
}
