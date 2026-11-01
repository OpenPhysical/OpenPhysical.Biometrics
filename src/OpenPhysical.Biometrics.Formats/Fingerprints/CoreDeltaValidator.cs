// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

// Validates the core-and-delta block of INCITS 378: a core group followed by a delta group.
internal static class CoreDeltaValidator
{
    private const int CoreGroup = 0;
    private const int DeltaGroup = 1;

    internal static UnitResult<FormatError> Validate(
        BinaryFormatReader reader,
        ImmutableArray<byte> data,
        int origin
    )
    {
        var scratch = reader.Scratch(data.AsMemory(), origin);
        return ReadGroup(scratch, CoreGroup)
            .Bind(_ => ReadGroup(scratch, DeltaGroup))
            .Bind(_ => scratch.RequireEnd());
    }

    private static Result<bool, FormatError> ReadGroup(BinaryFormatReader scratch, int group) =>
        scratch
            .Byte(group == CoreGroup ? "CoreInfo" : "DeltaInfo")
            .Check(info =>
                scratch.Require(
                    (info & 0x30) == 0 && (info >> 6) <= 1,
                    "CoreDeltaInfo",
                    "Core or delta information contains reserved bits.",
                    scratch.Offset - 1,
                    1
                )
            )
            .Bind(info =>
                ParseSteps
                    .Repeat<bool>(info & 15, _ => ReadPoint(scratch, group, info))
                    .Map(_ => true)
            );

    private static Result<bool, FormatError> ReadPoint(
        BinaryFormatReader scratch,
        int group,
        byte info
    )
    {
        var pointOffset = scratch.Offset;
        return (from x in scratch.UInt16("X") from y in scratch.UInt16("Y") select (x, y))
            .Check(point =>
                scratch.Require(
                    (point.x & 0xC000) == 0 && (point.y & 0xC000) == 0,
                    "CoreDeltaCoordinate",
                    "Core or delta coordinate contains reserved bits.",
                    pointOffset,
                    4
                )
            )
            .Bind(_ => HasAngles(group, info) ? ReadAngles(scratch, group) : true);
    }

    // Core and delta information use opposite angle-presence flags.
    private static bool HasAngles(int group, byte info) =>
        (group == CoreGroup && (info >> 6) == 1) || (group == DeltaGroup && (info >> 6) == 0);

    private static Result<bool, FormatError> ReadAngles(BinaryFormatReader scratch, int group) =>
        scratch
            .Read(group == CoreGroup ? 1 : 3, "Angles")
            .Check(angles =>
                scratch.Require(
                    angles.ToArray().All(angle => angle <= 179),
                    "CoreDeltaAngle",
                    "A core or delta angle exceeds 179 two-degree units.",
                    scratch.Offset - angles.Length,
                    angles.Length
                )
            )
            .Map(_ => true);
}
