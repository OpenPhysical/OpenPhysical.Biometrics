// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

internal sealed record Fs50HysteresisGrowth(
    int PrimarySeeds,
    int ContactArea,
    int RetainedArea,
    int UnsupportedExteriorArea,
    int LargestUnsupportedExteriorArea
);
