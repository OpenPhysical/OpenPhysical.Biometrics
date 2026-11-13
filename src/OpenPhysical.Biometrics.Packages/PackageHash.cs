// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using Org.BouncyCastle.Crypto.Digests;

internal static class PackageHash
{
    internal static string Of(ReadOnlySpan<byte> bytes)
    {
        var digest = new Sha256Digest();
        digest.BlockUpdate(bytes);
        var output = new byte[32];
        digest.DoFinal(output);
        return Convert.ToHexString(output).ToLowerInvariant();
    }
}
