// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class TemplateContactPlaneFiles
{
    internal static Result<ImmutableArray<TemplatePngDescriptor>, DesktopError> All(
        TemplateContactPlanes value
    )
    {
        var planes = ImmutableArray.Create(
            value.Candidate,
            value.RidgeSupport,
            value.Rejected,
            value.Accepted,
            value.Inner
        );
        return planes.All(TemplatePngFiles.Valid)
            ? planes
            : TemplatePngFiles.Invalid(
                "Advertised contact diagnostics require all five valid planes."
            );
    }
}
