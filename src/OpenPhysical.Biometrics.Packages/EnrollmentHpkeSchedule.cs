// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

internal static class EnrollmentHpkeSchedule
{
    private static readonly X9ECParameters Curve = ECNamedCurveTable.GetByName("secp256r1");

    internal static byte[] Encrypt(
        byte[] dh,
        byte[] enc,
        byte[] recipient,
        byte[] info,
        byte[] aad,
        byte[] plaintext
    )
    {
        var shared = EnrollmentHpkeDerivation.KemSharedSecret(dh, enc, recipient);
        var (key, nonce) = EnrollmentHpkeDerivation.KeySchedule(shared, info);
        try
        {
            return AesGcm(true, key, nonce, aad, plaintext);
        }
        finally
        {
            Array.Clear(shared);
            Array.Clear(key);
            Array.Clear(nonce);
        }
    }

    internal static byte[] Decrypt(
        byte[] dh,
        byte[] enc,
        byte[] recipient,
        byte[] info,
        byte[] aad,
        byte[] ciphertext
    )
    {
        var shared = EnrollmentHpkeDerivation.KemSharedSecret(dh, enc, recipient);
        var (key, nonce) = EnrollmentHpkeDerivation.KeySchedule(shared, info);
        try
        {
            return AesGcm(false, key, nonce, aad, ciphertext);
        }
        finally
        {
            Array.Clear(shared);
            Array.Clear(key);
            Array.Clear(nonce);
        }
    }

    internal static byte[] KemSharedSecret(byte[] dh, byte[] enc, byte[] recipient) =>
        EnrollmentHpkeDerivation.KemSharedSecret(dh, enc, recipient);

    internal static (byte[] Key, byte[] Nonce) VectorKeySchedule(
        byte[] shared,
        byte[] info,
        byte aeadId,
        int keyLength
    )
    {
        return EnrollmentHpkeDerivation.VectorKeySchedule(shared, info, aeadId, keyLength);
    }

    internal static byte[] VectorSeal(
        byte[] shared,
        byte[] info,
        byte[] aad,
        byte[] plaintext,
        byte aeadId,
        int keyLength
    )
    {
        var (key, nonce) = EnrollmentHpkeDerivation.VectorKeySchedule(
            shared,
            info,
            aeadId,
            keyLength
        );
        try
        {
            return AesGcm(true, key, nonce, aad, plaintext);
        }
        finally
        {
            Array.Clear(key);
            Array.Clear(nonce);
        }
    }

    private static byte[] AesGcm(bool encrypt, byte[] key, byte[] nonce, byte[] aad, byte[] input)
    {
        var cipher = new GcmBlockCipher(new AesEngine());
        cipher.Init(encrypt, new AeadParameters(new KeyParameter(key), 128, nonce, aad));
        var output = new byte[cipher.GetOutputSize(input.Length)];
        var done = false;
        try
        {
            var length = cipher.ProcessBytes(input, 0, input.Length, output, 0);
            length += cipher.DoFinal(output, length);
            done = true;
            return length == output.Length ? output : output[..length];
        }
        finally
        {
            if (!done)
                Array.Clear(output);
        }
    }

    internal static void RequireCurve(ECDomainParameters domain)
    {
        if (domain.Curve.FieldSize != 256 || !domain.Curve.Equals(Curve.Curve))
            throw new ArgumentException("Only the secp256r1 curve is supported.");
    }

    internal static void ValidatePublic(
        Org.BouncyCastle.Math.EC.ECPoint point,
        ECDomainParameters domain
    )
    {
        if (point.IsInfinity || !point.IsValid() || !point.Multiply(domain.N).IsInfinity)
            throw new ArgumentException("The P-256 public point is invalid.");
    }
}
