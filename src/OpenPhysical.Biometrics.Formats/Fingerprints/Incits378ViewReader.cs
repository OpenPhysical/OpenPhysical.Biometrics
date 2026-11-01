// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

// Reads finger views in wire order; each view owns its minutiae and extended data.
internal static class Incits378ViewReader
{
    internal static Result<ImmutableArray<FingerprintView>, FormatError> ReadViews(
        BinaryFormatReader reader,
        Incits378Header header
    ) =>
        ParseSteps.Repeat<FingerprintView>(
            header.ViewCount,
            previous => ReadView(reader, header, previous)
        );

    private static Result<FingerprintView, FormatError> ReadView(
        BinaryFormatReader reader,
        Incits378Header header,
        IReadOnlyList<FingerprintView> previous
    )
    {
        var path = $"Views[{previous.Count}]";
        return ReadViewHeader(reader, path, previous)
            .Bind(view =>
                Incits378MinutiaReader
                    .Read(reader, header, path, view.MinutiaCount)
                    .Bind(minutiae =>
                        Incits378ExtendedReader
                            .Read(reader, path, view.MinutiaCount)
                            .Map(blocks => view.ToView(minutiae, blocks))
                    )
            );
    }

    private static Result<Incits378ViewHeader, FormatError> ReadViewHeader(
        BinaryFormatReader reader,
        string path,
        IReadOnlyList<FingerprintView> previous
    )
    {
        var start = reader.Offset;
        return (
            from finger in reader.Byte(path + ".FingerPosition")
            from packed in reader.Byte(path + ".ViewAndImpression")
            from quality in reader.Byte(path + ".Quality")
            from count in reader.Byte(path + ".MinutiaCount")
            select new Incits378ViewHeader(finger, packed, quality, count)
        ).Check(view =>
            reader.Require(
                IsValidSelection(view, previous),
                "FingerView",
                "The finger view contains an invalid position, quality, or duplicate selection.",
                start,
                4
            )
        );
    }

    private static bool IsValidSelection(
        Incits378ViewHeader view,
        IReadOnlyList<FingerprintView> previous
    ) =>
        view.FingerPosition <= 10
        && view.ImpressionType is 0 or 1 or 2 or 3 or 8 or 9
        && Incits378Rules.IsValidFingerQuality(view.Quality)
        && !previous.Any(other =>
            other.FingerPosition == view.FingerPosition && other.ViewNumber == view.ViewNumber
        );
}
