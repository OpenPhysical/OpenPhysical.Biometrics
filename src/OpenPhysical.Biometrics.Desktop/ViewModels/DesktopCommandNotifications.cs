// SPDX-License-Identifier: GPL-2.0-or-later
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal static class DesktopCommandNotifications
{
    internal static UnitResult<DesktopError> Refresh(
        Action<string> changed,
        IEnumerable<string> properties,
        params IRelayCommand[] commands
    )
    {
        foreach (var property in properties)
            changed(property);
        foreach (var command in commands)
            command.NotifyCanExecuteChanged();
        return UnitResult.Success<DesktopError>();
    }
}
