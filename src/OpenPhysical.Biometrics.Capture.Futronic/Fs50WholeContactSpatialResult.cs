// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

internal sealed record Fs50WholeContactSpatialResult(
    Fs50WholeContactMetrics Metrics,
    bool SubstantialStatic
);
