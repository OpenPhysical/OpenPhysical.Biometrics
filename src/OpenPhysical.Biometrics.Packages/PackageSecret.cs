// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Buffers;
using System.Text;
using CSharpFunctionalExtensions;

/// <summary>Owns exact UTF-8 passphrase bytes and clears them when disposed.</summary>
public sealed class PackageSecret : IDisposable
{
    private readonly object gate = new();
    private byte[] bytes;

    private PackageSecret(byte[] bytes) => this.bytes = bytes;

    /// <summary>Creates an owned passphrase of at least fifteen Unicode scalar values without normalization.</summary>
    /// <param name="utf8">Exact UTF-8 samples, copied without trimming; caller owns its input.</param>
    /// <returns>An owned secret or a validation failure.</returns>
    public static Result<PackageSecret, PackageError> Create(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length > 4096)
            return Invalid();
        var remaining = utf8;
        var count = 0;
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf8(remaining, out _, out var consumed) != OperationStatus.Done)
                return Invalid();
            count++;
            remaining = remaining[consumed..];
        }
        return count < 15 ? Invalid() : new PackageSecret(utf8.ToArray());
    }

    internal Result<byte[], PackageError> Copy()
    {
        lock (gate)
            return bytes.Length == 0
                ? new PackageError(
                    PackageErrorCodes.SecretDisposed,
                    "Unlock the package before continuing."
                )
                : bytes.ToArray();
    }

    /// <summary>Clears owned passphrase bytes. This operation is idempotent.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            Array.Clear(bytes);
            bytes = [];
        }
    }

    private static PackageError Invalid() =>
        new(
            PackageErrorCodes.InvalidSecret,
            "Use at least fifteen Unicode characters encoded as valid UTF-8."
        );
}
