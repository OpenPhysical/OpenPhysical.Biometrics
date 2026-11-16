// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;

internal sealed record AlignmentInput(
    FingerprintRecord Record,
    FingerprintView View,
    AlignmentPoint[] Points
)
{
    internal static Result<AlignmentInput, FingerprintError> Create(
        FingerprintRecord record,
        int viewIndex
    )
    {
        if (record is null)
            return Invalid();
        if (record.Views.IsDefault || viewIndex < 0 || viewIndex >= record.Views.Length)
            return new FingerprintError(
                FingerprintErrorCodes.ViewSelection,
                "Select an existing reference and probe view."
            );
        var view = record.Views[viewIndex];
        if (
            view is null
            || record.XResolution == 0
            || record.YResolution == 0
            || record.Width == 0
            || record.Height == 0
            || view.Minutiae.IsDefault
            || view.Minutiae.Length is < 8 or > 255
        )
            return Invalid();
        if (
            view.Minutiae.Any(point =>
                point is null
                || point.X >= record.Width
                || point.Y >= record.Height
                || point.Type > 2
                || point.Angle > 179
                || point.Quality > 100
            )
        )
            return Invalid();
        var points = view
            .Minutiae.Select((point, index) => Normalize(point, index, record))
            .ToArray();
        return new AlignmentInput(record, view, points);
    }

    private static AlignmentPoint Normalize(
        FingerprintMinutia point,
        int index,
        FingerprintRecord record
    )
    {
        var radians = point.Angle * 2 * Math.PI / 180;
        return new(
            point.X * 197.0 / record.XResolution,
            point.Y * 197.0 / record.YResolution,
            Math.Atan2(
                -Math.Sin(radians) / record.YResolution,
                Math.Cos(radians) / record.XResolution
            ),
            point.Type,
            index
        );
    }

    private static FingerprintError Invalid() =>
        new(
            FingerprintErrorCodes.InvalidAlignmentInput,
            "Registration requires valid geometry and eight to 255 valid minutiae in each selected view."
        );
}
