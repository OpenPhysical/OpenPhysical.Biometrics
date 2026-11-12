// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

internal sealed record TemplateContactPlanes(
    TemplatePngDescriptor Candidate,
    TemplatePngDescriptor RidgeSupport,
    TemplatePngDescriptor Rejected,
    TemplatePngDescriptor Accepted,
    TemplatePngDescriptor Inner
);
