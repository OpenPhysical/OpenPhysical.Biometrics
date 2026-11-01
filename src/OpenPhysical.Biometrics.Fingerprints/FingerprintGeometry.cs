// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

using MatchingMinutia = OpenPhysical.Biometrics.NBIS.FingerprintMinutia;
using StandardMinutia = OpenPhysical.Biometrics.Formats.Fingerprints.FingerprintMinutia;

// Both directions share the same coordinate origin and angle convention here.
// INCITS uses top-left pixels and two-degree angles; NBIS uses height-minus-row
// coordinates and reverses the ridge direction by half a turn.
internal static class FingerprintGeometry
{
    internal const ushort NominalPixelsPerCentimeter = 197;

    internal static bool IsValid(StandardMinutia point, int width, int height) =>
        point.X < width
        && point.Y < height
        && point.Angle <= 179
        && point.Quality <= 100
        && point.Type <= 2;

    // The caller has checked IsValid.
    internal static MatchingMinutia ToMatching(StandardMinutia point, int height) =>
        new(
            point.X,
            height - point.Y,
            ReverseDirection(point.Angle * 2),
            point.Quality,
            point.Type
        );

    // NBIS templates guarantee coordinates, angles, quality, and type within these ranges.
    internal static StandardMinutia ToRecord(MatchingMinutia point, int height) =>
        new(
            (byte)point.Type,
            (ushort)point.X,
            (ushort)(height - point.Y),
            (byte)(ReverseDirection(point.Angle) / 2),
            (byte)point.Quality
        );

    private static int ReverseDirection(int degrees) => (degrees + 180) % 360;
}
