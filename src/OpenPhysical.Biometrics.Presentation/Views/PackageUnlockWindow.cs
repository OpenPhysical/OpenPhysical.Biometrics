// SPDX-License-Identifier: GPL-2.0-or-later
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class PackageUnlockWindow : Window
{
    private readonly PackageProtectionViewModel protection;
    private readonly PropertyChangedEventHandler changed;

    internal PackageUnlockWindow(PackageProtectionViewModel protection)
    {
        this.protection = protection;
        AvaloniaXamlLoader.Load(this);
        DataContext = protection;
        changed = (_, args) => Updated(args);
        protection.PropertyChanged += changed;
        Closed += (_, _) =>
        {
            protection.PropertyChanged -= changed;
            protection.ClearInputs();
        };
    }

    private void Updated(PropertyChangedEventArgs args)
    {
        if (
            args.PropertyName == nameof(PackageProtectionViewModel.IsUnlocked)
            && protection.IsUnlocked
        )
            Close(true);
    }

    private void Cancel(object sender, RoutedEventArgs args) => Close(false);
}
