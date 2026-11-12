// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed record CapturePolicyParts(
    EnhancementSettings Enhancement,
    Fs50EvidencePolicy Evidence,
    Fs50EmptySensorPolicy EmptySensor,
    FingerprintQualitySelectionPolicy Quality,
    Fs50ContactContourPolicy Contour,
    Fs50WholeContactPolicy WholeContact
);
