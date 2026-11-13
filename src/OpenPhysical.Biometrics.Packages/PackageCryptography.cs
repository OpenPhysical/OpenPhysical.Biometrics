// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Buffers.Binary;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

internal static class PackageCryptography
{
    private static readonly TaskFactory KdfTasks = new(TaskScheduler.Default);

    internal static Result<byte[], PackageError> Transform(
        byte[] input,
        byte[] header,
        PackageSecret secret,
        bool encrypt
    )
    {
        var password = secret.Copy();
        if (password.IsFailure)
            return password.Error;
        var key = new byte[32];
        try
        {
            return PackageEdge.Try(
                () =>
                {
                    Derive(password.Value, header, key);
                    return Cipher(input, header, key, encrypt);
                },
                PackageErrorCodes.CryptoFailed,
                "The cryptographic provider could not process the package."
            );
        }
        finally
        {
            Array.Clear(password.Value);
            Array.Clear(key);
        }
    }

    private static int Derive(byte[] password, byte[] header, byte[] key)
    {
        var builder = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
            .WithVersion(Argon2Parameters.Version13)
            .WithMemoryAsKB((int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16)))
            .WithIterations((int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20)))
            .WithParallelism((int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(24)))
            .WithSalt(header.AsSpan(36, 16).ToArray());
        try
        {
            var generator = new Argon2BytesGenerator(KdfTasks);
            generator.Init(builder.Build());
            return generator.GenerateBytes(password, key);
        }
        finally
        {
            builder.Clear();
        }
    }

    private static byte[] Cipher(byte[] input, byte[] header, byte[] key, bool encrypt)
    {
        var cipher = new GcmBlockCipher(new AesEngine());
        cipher.Init(
            encrypt,
            new AeadParameters(new KeyParameter(key), 128, header.AsSpan(52, 12).ToArray(), header)
        );
        var output = new byte[cipher.GetOutputSize(input.Length)];
        var complete = false;
        try
        {
            var used = cipher.ProcessBytes(input, 0, input.Length, output, 0);
            cipher.DoFinal(output, used);
            complete = true;
            return output;
        }
        finally
        {
            if (!complete)
                Array.Clear(output);
        }
    }
}
