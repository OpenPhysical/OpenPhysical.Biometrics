// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed record TemplateContourPolicyMetadata(int ContextMargin, int ModifiedBoundaryGuard)
{
    internal static TemplateContourPolicyMetadata From(Fs50ContactContourPolicy value) =>
        new(value.Options.ContextMargin, value.Options.ModifiedBoundaryGuard);

    internal Result<Fs50ContactContourPolicy, DesktopError> Read() =>
        Fs50ContactContourPolicy
            .Create(
                new Fs50ContactContourOptions
                {
                    ContextMargin = ContextMargin,
                    ModifiedBoundaryGuard = ModifiedBoundaryGuard,
                }
            )
            .MapError(error => TemplatePngFiles.Invalid(error.Message));
}
