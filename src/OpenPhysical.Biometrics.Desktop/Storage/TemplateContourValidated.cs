// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed record TemplateContourValidated(
    TemplateContourManifest Manifest,
    Fs50ContactContourPolicy Policy,
    TemplateContactManifest Contact
);
