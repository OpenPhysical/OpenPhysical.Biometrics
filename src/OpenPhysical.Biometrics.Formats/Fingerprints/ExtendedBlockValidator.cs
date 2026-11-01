// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

// Checks the structure of recognized INCITS 378 extended blocks. Vendor blocks pass unchanged.
internal static class ExtendedBlockValidator
{
    internal static UnitResult<FormatError> Validate(
        BinaryFormatReader reader,
        ushort type,
        ImmutableArray<byte> data,
        int minutiaCount,
        int origin
    ) =>
        reader
            .Require(
                !Incits378Rules.IsReservedBlockType(type),
                "ExtendedBlockType",
                "The extended block uses a reserved type identifier.",
                origin - 4,
                2
            )
            .Bind(() =>
                type switch
                {
                    1 => RidgeCountValidator.Validate(reader, data, minutiaCount, origin),
                    2 => CoreDeltaValidator.Validate(reader, data, origin),
                    _ => UnitResult.Success<FormatError>(),
                }
            );
}
