// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class EnrollmentSectionNavigation(
    IEnrollmentSection initial,
    Func<UnitResult<DesktopError>> changed
)
{
    internal IEnrollmentSection Selected { get; private set; } = initial;
    internal bool Changing { get; private set; }
    internal bool Active { get; private set; }
    internal bool Stopping { get; set; }
    internal string Problem { get; private set; } = string.Empty;

    internal Task<UnitResult<DesktopError>> ActivateAsync(bool visible)
    {
        Active = visible;
        return Stopping
            ? Task.FromResult(UnitResult.Success<DesktopError>())
            : Selected.ActivateAsync(visible);
    }

    internal async Task<UnitResult<DesktopError>> SelectAsync(IEnrollmentSection next)
    {
        if (Stopping || Changing)
            return UnitResult.Success<DesktopError>();
        Changing = true;
        changed();
        try
        {
            var paused = await Selected.ActivateAsync(false);
            if (paused.IsFailure)
            {
                Problem = paused.Error.Message;
                return paused;
            }
            Selected = next;
            Problem = string.Empty;
            var activated = await Selected.ActivateAsync(Active);
            if (activated.IsFailure)
                Problem = activated.Error.Message;
            return activated;
        }
        finally
        {
            Changing = false;
            changed();
        }
    }
}
