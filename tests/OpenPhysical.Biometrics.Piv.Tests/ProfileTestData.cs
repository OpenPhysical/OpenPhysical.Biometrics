// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv.Tests;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Formats.Fingerprints;

// Valid records for profile tests. Each test corrupts one field of a fresh value.
internal static class ProfileTestData
{
    internal static readonly DateTimeOffset EvaluationTime = new(
        2026,
        1,
        3,
        0,
        0,
        0,
        TimeSpan.Zero
    );

    internal static FingerprintRecord Fingerprints() =>
        new(1, 1, 0x8001, 512, 512, 197, 197, [View(2), View(7)]);

    internal static FingerprintView View(byte finger) =>
        new(finger, 0, 0, 80, [new FingerprintMinutia(1, 10, 20, 30, 90)], []);

    internal static FaceRecord Face() =>
        new([
            new FaceImage(new(0, 0, 0, 0, 1, 0, 0), [], new(1, 1, 640, 480, 1, 2, 0, 0), [1, 2, 3]),
        ]);

    internal static CbeffRecord Wrapped(
        FingerprintRecord record,
        sbyte quality = 80,
        bool earlyValidity = false
    ) => WrappedWithCreator(record, new byte[18], quality, earlyValidity);

    internal static CbeffRecord WrappedWithCreator(
        FingerprintRecord record,
        byte[] creator,
        sbyte quality = 80,
        bool earlyValidity = false
    )
    {
        var data = Incits378Codec.Write(record).Value;
        var acquired = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var header = CbeffHeader
            .Create(
                (uint)data.Length,
                1,
                0x001B,
                0x0201,
                acquired,
                earlyValidity ? acquired.AddDays(-1) : acquired,
                acquired.AddYears(1),
                8,
                0x80,
                quality,
                creator,
                new byte[25],
                securityOptions: 0x0D
            )
            .Value;
        return new CbeffRecord(header, ImmutableArray.Create(data), [1]);
    }
}
