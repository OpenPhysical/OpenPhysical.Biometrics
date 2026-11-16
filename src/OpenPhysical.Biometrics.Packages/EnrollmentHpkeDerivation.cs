// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Text;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

internal static class EnrollmentHpkeDerivation
{
    private static readonly byte[] HpkeVersion = "HPKE-v1"u8.ToArray();
    private static readonly byte[] KemSuite = [.. "KEM"u8, 0x00, 0x10];

    internal static byte[] KemSharedSecret(byte[] dh, byte[] enc, byte[] recipient)
    {
        var eaePrk = LabeledExtract(KemSuite, [], "eae_prk", dh);
        var context = Join(enc, recipient);
        try
        {
            return LabeledExpand(KemSuite, eaePrk, "shared_secret", context, 32);
        }
        finally
        {
            Array.Clear(eaePrk);
            Array.Clear(context);
            Array.Clear(dh);
        }
    }

    internal static (byte[] Key, byte[] Nonce) KeySchedule(byte[] shared, byte[] info) =>
        VectorKeySchedule(shared, info, 0x02, 32);

    internal static (byte[] Key, byte[] Nonce) VectorKeySchedule(
        byte[] shared,
        byte[] info,
        byte aeadId,
        int keyLength
    )
    {
        var suite = Suite(aeadId);
        var pskIdHash = LabeledExtract(suite, [], "psk_id_hash", []);
        var infoHash = LabeledExtract(suite, [], "info_hash", info);
        var context = Join([0], pskIdHash, infoHash);
        var secret = LabeledExtract(suite, shared, "secret", []);
        try
        {
            return (
                LabeledExpand(suite, secret, "key", context, keyLength),
                LabeledExpand(suite, secret, "base_nonce", context, 12)
            );
        }
        finally
        {
            Array.Clear(shared);
            Array.Clear(pskIdHash);
            Array.Clear(infoHash);
            Array.Clear(context);
            Array.Clear(secret);
        }
    }

    private static byte[] Suite(byte aeadId) => [.. "HPKE"u8, 0x00, 0x10, 0x00, 0x01, 0x00, aeadId];

    private static byte[] LabeledExtract(byte[] suite, byte[] salt, string label, byte[] input) =>
        HkdfExtract(salt, Join(HpkeVersion, suite, Encoding.ASCII.GetBytes(label), input));

    private static byte[] LabeledExpand(
        byte[] suite,
        byte[] prk,
        string label,
        byte[] info,
        int length
    ) =>
        HkdfExpand(
            prk,
            Join(
                [(byte)(length >> 8), (byte)length],
                HpkeVersion,
                suite,
                Encoding.ASCII.GetBytes(label),
                info
            ),
            length
        );

    private static byte[] HkdfExtract(byte[] salt, byte[] input)
    {
        var mac = new HMac(new Sha256Digest());
        var actualSalt = salt.Length == 0 ? new byte[32] : salt;
        mac.Init(new KeyParameter(actualSalt));
        mac.BlockUpdate(input);
        var output = new byte[32];
        mac.DoFinal(output);
        Array.Clear(input);
        if (salt.Length == 0)
            Array.Clear(actualSalt);
        return output;
    }

    private static byte[] HkdfExpand(byte[] prk, byte[] info, int length)
    {
        var result = new byte[length];
        var previous = Array.Empty<byte>();
        var offset = 0;
        for (byte block = 1; offset < length; block++)
        {
            var mac = new HMac(new Sha256Digest());
            mac.Init(new KeyParameter(prk));
            mac.BlockUpdate(Join(previous, info, [block]));
            var next = new byte[32];
            mac.DoFinal(next);
            Array.Clear(previous);
            previous = next;
            var count = Math.Min(next.Length, length - offset);
            next.AsSpan(0, count).CopyTo(result.AsSpan(offset));
            offset += count;
        }
        Array.Clear(previous);
        Array.Clear(info);
        return result;
    }

    private static byte[] Join(params byte[][] chunks)
    {
        var result = new byte[chunks.Sum(value => value.Length)];
        var offset = 0;
        foreach (var chunk in chunks)
        {
            chunk.CopyTo(result, offset);
            offset += chunk.Length;
        }
        return result;
    }
}
