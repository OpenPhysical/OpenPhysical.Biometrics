// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

internal sealed class EnrollmentFingerprintCommands
{
    internal EnrollmentFingerprintCommands(
        Func<Task> accept,
        Func<bool> canAccept,
        Func<Task> reset,
        Func<Task> primary,
        Func<Task> secondary,
        Func<Task> adjust,
        Func<bool> canSelect,
        Func<bool> pair,
        Func<bool> accepted
    )
    {
        Accept = new AsyncRelayCommand(accept, canAccept);
        Reset = new AsyncRelayCommand(reset, canSelect);
        Primary = new AsyncRelayCommand(primary, canSelect);
        Secondary = new AsyncRelayCommand(secondary, () => canSelect() && pair());
        Adjust = new AsyncRelayCommand(adjust, () => canSelect() && accepted());
        Accept.PropertyChanged += (_, _) => Notify();
    }

    internal IAsyncRelayCommand Accept { get; }
    internal IAsyncRelayCommand Reset { get; }
    internal IAsyncRelayCommand Primary { get; }
    internal IAsyncRelayCommand Secondary { get; }
    internal IAsyncRelayCommand Adjust { get; }

    internal UnitResult<DesktopError> Notify()
    {
        Accept.NotifyCanExecuteChanged();
        Reset.NotifyCanExecuteChanged();
        Primary.NotifyCanExecuteChanged();
        Secondary.NotifyCanExecuteChanged();
        Adjust.NotifyCanExecuteChanged();
        return UnitResult.Success<DesktopError>();
    }
}
