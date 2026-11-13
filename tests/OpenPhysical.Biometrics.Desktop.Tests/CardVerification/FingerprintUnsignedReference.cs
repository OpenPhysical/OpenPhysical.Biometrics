// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using System.Text;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.Piv;

namespace OpenPhysical.Middleware.Tests;

internal static class FingerprintUnsignedReference
{
    internal static CbeffRecord Create(BiometricIdentity identity)
    {
        var fmr = RecordBytes();
        return new(Header(fmr.Length, identity), fmr.ToImmutableArray(), []);
    }

    private static byte[] RecordBytes()
    {
        var points = Enumerable
            .Range(0, 12)
            .Select(index => new FingerprintMinutia(1, (ushort)(index + 1), 20, 0, 70))
            .ToImmutableArray();
        var record = new FingerprintRecord(
            1,
            2,
            0x8003,
            128,
            128,
            197,
            197,
            [new(2, 0, 0, 80, points, []), new(7, 0, 0, 80, points, [])]
        );
        return Incits378Codec.Write(record).Value;
    }

    internal static CbeffHeader Header(int length, BiometricIdentity identity, sbyte quality = 80)
    {
        var now = VerificationBundleFixture.Now;
        return CbeffHeader
            .Create(
                (uint)length,
                0,
                0x001b,
                0x0201,
                now.AddMinutes(-1),
                now.AddMinutes(-1),
                now.AddDays(1),
                8,
                0x80,
                quality,
                Encoding.ASCII.GetBytes("OpenPhysical Test\0"),
                identity.FascN.AsSpan(),
                securityOptions: 0x0d
            )
            .Value;
    }
}
