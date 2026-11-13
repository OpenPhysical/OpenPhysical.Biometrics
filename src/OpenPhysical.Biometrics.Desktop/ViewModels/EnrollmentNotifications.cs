// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

internal static class EnrollmentNotifications
{
    internal static readonly string[] Properties =
    [
        nameof(EnrollmentViewModel.IsBusy),
        nameof(EnrollmentViewModel.IsEditing),
        nameof(EnrollmentViewModel.CanNavigate),
        nameof(EnrollmentViewModel.CanReset),
        nameof(EnrollmentViewModel.Progress),
        nameof(EnrollmentViewModel.Instructions),
        nameof(EnrollmentViewModel.Ready),
    ];
}
