// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Tests;

using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Formats.Fingerprints;

// Shared inputs for codec tests. Each value is built fresh so tests may corrupt it freely.
internal static class CodecTestData
{
    // This hand-authored record has one right-index view and one ridge ending.
    // It checks the byte layout independently of the writer under test.
    internal static byte[] FingerprintBytes() =>
        Convert.FromHexString(
            "464D52002032300000260001000280010200020000C500C5010002005001400A00142D640000"
        );

    internal static FingerprintRecord Fingerprint() =>
        Incits378Codec.Parse(FingerprintBytes()).Value.Record;

    internal static FaceRecord Face()
    {
        var face = new FaceImage(
            new(0, 0, 0, 0, 1, 0, 0),
            [new(1, 1, 10, 20)],
            new(1, 0, 256, 256, 1, 2, 0, 0xFFFF),
            [1, 2, 3]
        );
        return new([face, face]);
    }

    internal static CbeffRecord Cbeff()
    {
        var time = new DateTimeOffset(2026, 10, 31, 12, 34, 56, TimeSpan.Zero);
        var header = CbeffHeader
            .Create(
                3,
                2,
                0x001B,
                0x0201,
                time,
                time,
                time.AddYears(1),
                8,
                0,
                0,
                new byte[18],
                new byte[25]
            )
            .Value;
        return new(header, [1, 2, 3], [4, 5]);
    }
}
