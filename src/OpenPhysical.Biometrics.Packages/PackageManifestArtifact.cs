// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

internal sealed record PackageManifestArtifact(
    string Name,
    string Role,
    string Format,
    long Length,
    string Sha256
);
