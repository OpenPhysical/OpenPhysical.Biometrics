// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

// Reads the length-prefixed extended-data area of one view as a sequence of typed blocks.
internal static class Incits378ExtendedReader
{
    internal static Result<ImmutableArray<FingerprintExtendedBlock>, FormatError> Read(
        BinaryFormatReader reader,
        string path,
        int minutiaCount
    ) =>
        reader
            .UInt16(path + ".ExtendedLength")
            .Bind(length => reader.Block(length, path + ".ExtendedData"))
            .Bind(area =>
                ParseSteps.Until<FingerprintExtendedBlock>(
                    _ => area.Remaining == 0,
                    done =>
                        ReadBlock(
                            area,
                            reader,
                            $"{path}.ExtendedBlocks[{done.Count}]",
                            minutiaCount
                        )
                )
            );

    private static Result<FingerprintExtendedBlock, FormatError> ReadBlock(
        BinaryFormatReader area,
        BinaryFormatReader reader,
        string blockPath,
        int minutiaCount
    )
    {
        var blockOffset = area.Offset;
        return (
            from type in area.UInt16(blockPath + ".Type")
            from length in area.UInt16(blockPath + ".Length")
            select new { type, length }
        )
            .Ensure(
                block => block.length >= 4,
                _ =>
                    reader.Error(
                        "ExtendedBlockLength",
                        "An extended block length must include its four-byte header.",
                        blockOffset + 2,
                        2
                    )
            )
            .Bind(block =>
                area.Bytes(block.length - 4, blockPath + ".Data")
                    .Check(data =>
                        ExtendedBlockValidator.Validate(
                            reader,
                            block.type,
                            data,
                            minutiaCount,
                            blockOffset + 4
                        )
                    )
                    .Map(data => new FingerprintExtendedBlock(block.type, data))
            );
    }
}
