// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

internal static class EnrollmentCommandTasks
{
    internal static bool CanClear(EnrollmentViewModel enrollment) =>
        (!enrollment.Fingerprints.IsBusy || enrollment.Fingerprints.Adjustment.IsOpen)
        && (
            !enrollment.Fingerprints.Adjustment.IsOpen
            || enrollment.Fingerprints.Adjustment.CancelCommand.CanExecute(default)
        );

    internal static Task<UnitResult<DesktopError>> DrainAsync(
        EnrollmentViewModel enrollment,
        IAsyncRelayCommand save,
        IAsyncRelayCommand clear
    ) =>
        DesktopCommandTasks.DrainAsync(
            save,
            enrollment.AcceptCommand,
            enrollment.Fingerprints.PrimarySlotCommand,
            enrollment.Fingerprints.SecondarySlotCommand,
            enrollment.Fingerprints.ResetCaptureCommand,
            enrollment.Fingerprints.AdjustCaptureCommand,
            enrollment.Fingerprints.Adjustment.ApplyCommand,
            enrollment.Fingerprints.Adjustment.CancelCommand,
            clear
        );
}
