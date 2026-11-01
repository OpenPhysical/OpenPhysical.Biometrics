// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

// Validates the ridge-count block of INCITS 378: a method byte followed by three-byte entries.
internal static class RidgeCountValidator
{
    internal static UnitResult<FormatError> Validate(
        BinaryFormatReader reader,
        ImmutableArray<byte> data,
        int minutiaCount,
        int origin
    ) =>
        reader
            .Require(
                data.Length >= 1 && data[0] <= 2 && (data.Length - 1) % 3 == 0,
                "RidgeCountBlock",
                "The ridge-count block has an invalid extraction method or entry length.",
                origin,
                data.Length
            )
            .Bind(() => ValidateEntries(reader, data, minutiaCount, origin));

    private static UnitResult<FormatError> ValidateEntries(
        BinaryFormatReader reader,
        ImmutableArray<byte> data,
        int minutiaCount,
        int origin
    ) =>
        Enumerable
            .Range(0, (data.Length - 1) / 3)
            .Select(entry => 1 + 3 * entry)
            .Where(index => !IsValidEntry(data, index, minutiaCount))
            .TryFirst()
            .Match(
                index =>
                    UnitResult.Failure(
                        reader.Error(
                            "RidgeCountIndex",
                            "A ridge-count entry refers to a minutia outside this view.",
                            origin + index,
                            3
                        )
                    ),
                UnitResult.Success<FormatError>
            );

    private static bool IsValidEntry(ImmutableArray<byte> data, int index, int minutiaCount)
    {
        var first = data[index];
        var second = data[index + 1];
        var third = data[index + 2];
        var unused = first == 0 && second == 0 && third == 0;
        var singleMinutia =
            data[0] is 1 or 2 && first > 0 && first <= minutiaCount && second == 0 && third == 0;
        var pair = first != 0 && second != 0 && first <= minutiaCount && second <= minutiaCount;
        return unused || singleMinutia || pair;
    }
}
