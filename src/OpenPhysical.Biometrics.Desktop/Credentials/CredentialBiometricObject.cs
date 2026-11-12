// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal static class CredentialBiometricObject
{
    internal static Result<byte[], DesktopError> Read(byte[] bytes) =>
        Value(bytes, 0x53)
            .Bind(outer =>
                outer.Consumed == bytes.Length
                    ? Value(outer.Value, 0xBC)
                        .Ensure(
                            inner =>
                                inner.Consumed == outer.Value.Length
                                || outer
                                    .Value.AsSpan(inner.Consumed)
                                    .SequenceEqual(new byte[] { 0xFE, 0 }),
                            new DesktopError(
                                "Credential.Object",
                                "The fingerprint object contains unsupported trailing data."
                            )
                        )
                        .Map(inner => inner.Value)
                    : Result.Failure<byte[], DesktopError>(
                        new DesktopError(
                            "Credential.Object",
                            "The fingerprint object length is invalid."
                        )
                    )
            );

    private static Result<(byte[] Value, int Consumed), DesktopError> Value(byte[] bytes, byte tag)
    {
        if (bytes.Length < 2 || bytes[0] != tag)
            return new DesktopError(
                "Credential.Object",
                "The fingerprint object has an unexpected tag."
            );
        var offset = 2;
        var length = (int)bytes[1];
        if ((length & 128) != 0)
        {
            var size = length & 127;
            if (size is < 1 or > 3 || bytes.Length < 2 + size)
                return new DesktopError(
                    "Credential.Object",
                    "The fingerprint object uses an invalid BER length."
                );
            length = 0;
            for (var i = 0; i < size; i++)
                length = (length << 8) | bytes[offset++];
        }
        return length <= bytes.Length - offset
            ? (bytes.AsSpan(offset, length).ToArray(), offset + length)
            : new DesktopError("Credential.Object", "The fingerprint object is truncated.");
    }
}
