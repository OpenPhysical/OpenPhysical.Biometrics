// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;

internal sealed record TemplateContourPlaneFiles(
    TemplatePngDescriptor Contact,
    TemplatePngDescriptor FilledHole,
    TemplatePngDescriptor Feature,
    TemplatePngDescriptor Support,
    TemplatePngDescriptor Context,
    TemplatePngDescriptor Uncertain,
    TemplatePngDescriptor GuardedContext,
    TemplatePngDescriptor ObservedContact
)
{
    internal ImmutableArray<TemplatePngDescriptor> Additional =>
        [Contact, FilledHole, Feature, Support, Uncertain, GuardedContext, ObservedContact];

    internal bool Valid(TemplateProcessingMasks processing) =>
        Additional.All(TemplatePngFiles.Valid)
        && TemplatePngFiles.Valid(Context)
        && Context == processing.Retained;
}
