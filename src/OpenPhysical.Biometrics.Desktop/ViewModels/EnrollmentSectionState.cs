// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CommunityToolkit.Mvvm.ComponentModel;

// Sidebar bindings observe the stable section object, not a rebuilt collection.
internal abstract class EnrollmentSectionState : ObservableObject
{
    protected EnrollmentSectionState(ObservableObject viewModel) =>
        viewModel.PropertyChanged += (_, change) => OnPropertyChanged(change.PropertyName);
}
