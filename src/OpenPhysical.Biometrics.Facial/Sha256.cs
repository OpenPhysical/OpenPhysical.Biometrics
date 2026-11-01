// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Utilities.Encoders;

internal static class Sha256
{
    internal static string HexDigest(ReadOnlySpan<byte> data)
    {
        var digest = new Sha256Digest();
        digest.BlockUpdate(data);
        var hash = new byte[digest.GetDigestSize()];
        _ = digest.DoFinal(hash);
        return Hex.ToHexString(hash);
    }
}
