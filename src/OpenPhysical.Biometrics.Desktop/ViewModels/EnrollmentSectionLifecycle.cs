// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal static class EnrollmentSectionLifecycle
{
    internal static Task<UnitResult<DesktopError>> ResetAsync(
        ImmutableArray<IEnrollmentSection> sections
    ) => RunAsync(sections, ResetSectionAsync);

    private static async Task<UnitResult<DesktopError>> ResetSectionAsync(
        IEnrollmentSection section
    )
    {
        var paused = await section.ActivateAsync(false);
        var reset = await section.ResetAsync();
        return paused.IsFailure ? paused : reset;
    }

    internal static Task<UnitResult<DesktopError>> StopAsync(
        ImmutableArray<IEnrollmentSection> sections
    ) => RunAsync(sections, section => section.StopAsync());

    private static async Task<UnitResult<DesktopError>> RunAsync(
        ImmutableArray<IEnrollmentSection> sections,
        Func<IEnrollmentSection, Task<UnitResult<DesktopError>>> action
    )
    {
        var outcome = UnitResult.Success<DesktopError>();
        foreach (var section in sections)
        {
            var result = await action(section);
            if (result.IsFailure && outcome.IsSuccess)
                outcome = result;
        }
        return outcome;
    }
}
