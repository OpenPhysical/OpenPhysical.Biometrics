// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

internal readonly record struct AlignmentPoint(
    double X,
    double Y,
    double Direction,
    byte Type,
    int Index
);
