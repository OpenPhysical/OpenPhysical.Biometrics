// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

namespace OpenPhysical.Biometrics.Desktop;

internal static class EnrollmentSectionArtifacts
{
    internal static Result<ImmutableArray<PackageArtifact>, DesktopError> Prepare(
        ImmutableArray<IEnrollmentSection> sections,
        bool ready
    )
    {
        if (!ready)
            return new DesktopError(
                "Session.Incomplete",
                "Accept every required capture before saving."
            );
        var result = ImmutableArray.CreateBuilder<PackageArtifact>();
        foreach (var section in sections)
        {
            var prepared = section.PrepareArtifacts();
            if (prepared.IsFailure)
                return prepared.Error;
            result.AddRange(prepared.Value);
        }
        return result.ToImmutable();
    }
}
