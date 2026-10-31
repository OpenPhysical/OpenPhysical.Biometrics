// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using MatchingMinutia = OpenPhysical.Biometrics.NBIS.FingerprintMinutia;
using StandardMinutia = OpenPhysical.Biometrics.Formats.Fingerprints.FingerprintMinutia;

// Both directions share the same coordinate origin and angle convention here.
// INCITS uses top-left pixels and two-degree angles; NBIS uses height-minus-row
// coordinates and reverses the ridge direction by half a turn.
internal static class FingerprintGeometry
{
    internal const ushort NominalPixelsPerCentimeter = 197;

    internal static MatchingMinutia ToMatching(StandardMinutia point, int width, int height)
    {
        if (
            point is null
            || point.X >= width
            || point.Y >= height
            || point.Angle > 179
            || point.Quality > 100
            || point.Type > 2
        )
            throw new ArgumentException(
                "The selected view contains invalid INCITS 378 minutiae.",
                nameof(point)
            );
        return new(
            point.X,
            height - point.Y,
            ReverseDirection(point.Angle * 2),
            point.Quality,
            point.Type
        );
    }

    internal static StandardMinutia ToRecord(MatchingMinutia point, int height) =>
        new(
            checked((byte)point.Type),
            checked((ushort)point.X),
            checked((ushort)(height - point.Y)),
            checked((byte)(ReverseDirection(point.Angle) / 2)),
            checked((byte)point.Quality)
        );

    private static int ReverseDirection(int degrees) => (degrees + 180) % 360;
}
