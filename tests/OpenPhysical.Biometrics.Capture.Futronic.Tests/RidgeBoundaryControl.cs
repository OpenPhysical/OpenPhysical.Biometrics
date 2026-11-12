// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

internal sealed record RidgeBoundaryControl(
    string Name,
    string Requirement,
    byte[] Pixels,
    byte[] Reference,
    bool PrimaryOnly = false
);
