// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

internal sealed class EnrollmentStructure(
    ImmutableArray<IEnrollmentSection> registered,
    IEnrollmentSection initial,
    Action<string> changed
)
{
    private IEnrollmentSection selected = initial;
    private Maybe<CapturePlanChoice> plan;
    internal ImmutableArray<IEnrollmentSection> Required { get; private set; } = [];

    internal UnitResult<DesktopError> Plan(CapturePlanChoice value)
    {
        if (plan.Match(current => current == value, () => false))
            return UnitResult.Success<DesktopError>();
        plan = value;
        Required = [.. registered.Where(section => value.Includes(section.Id))];
        changed(nameof(EnrollmentViewModel.Plan));
        changed(nameof(EnrollmentViewModel.Sections));
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Refresh(
        IEnrollmentSection value,
        params IRelayCommand[] commands
    )
    {
        Selection(value);
        return DesktopCommandNotifications.Refresh(
            changed,
            EnrollmentNotifications.Properties,
            commands
        );
    }

    internal UnitResult<DesktopError> Selection(IEnrollmentSection value)
    {
        if (!ReferenceEquals(value, selected))
        {
            selected = value;
            changed(nameof(EnrollmentViewModel.ActiveSection));
            changed(nameof(EnrollmentViewModel.FaceActive));
            changed(nameof(EnrollmentViewModel.FingerprintsActive));
        }
        return UnitResult.Success<DesktopError>();
    }
}
