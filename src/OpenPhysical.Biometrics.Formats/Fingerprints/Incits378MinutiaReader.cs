// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

// Reads the fixed six-byte minutiae of one view and checks them against image geometry.
internal static class Incits378MinutiaReader
{
    internal static Result<ImmutableArray<FingerprintMinutia>, FormatError> Read(
        BinaryFormatReader reader,
        Incits378Header header,
        string path,
        int count
    ) =>
        ParseSteps.Repeat<FingerprintMinutia>(
            count,
            done => ReadMinutia(reader, header, $"{path}.Minutiae[{done.Count}]")
        );

    private static Result<FingerprintMinutia, FormatError> ReadMinutia(
        BinaryFormatReader reader,
        Incits378Header header,
        string prefix
    )
    {
        var offset = reader.Offset;
        return (
            from x in reader.UInt16(prefix + ".TypeAndX")
            from y in reader.UInt16(prefix + ".Y")
            from angle in reader.Byte(prefix + ".Angle")
            from quality in reader.Byte(prefix + ".Quality")
            select new
            {
                x,
                y,
                angle,
                quality,
            }
        )
            .Check(point =>
                reader.Require(
                    IsValid(point.x, point.y, point.angle, point.quality, header),
                    "Minutia",
                    "A minutia has an invalid type, coordinate, angle, quality, or reserved bit.",
                    offset,
                    6
                )
            )
            .Map(point => new FingerprintMinutia(
                (byte)(point.x >> 14),
                (ushort)(point.x & 0x3FFF),
                point.y,
                point.angle,
                point.quality
            ));
    }

    private static bool IsValid(
        ushort x,
        ushort y,
        byte angle,
        byte quality,
        Incits378Header header
    ) =>
        (x >> 14) != 3
        && (y & 0xC000) == 0
        && (x & 0x3FFF) < header.Width
        && y < header.Height
        && angle <= 179
        && quality <= 100;
}
