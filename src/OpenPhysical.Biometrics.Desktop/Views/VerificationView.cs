// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

/// <summary>Reference selection and fingerprint comparison workspace.</summary>
public sealed class VerificationView : UserControl
{
    /// <summary>Creates the comparison workspace.</summary>
    public VerificationView() => AvaloniaXamlLoader.Load(this);

    private async void ReferenceSourceChanged(object sender, SelectionChangedEventArgs args)
    {
        if (
            sender is not TabControl tabs
            || !ReferenceEquals(args.Source, tabs)
            || DataContext is not WorkspaceViewModel workspace
        )
            return;
        workspace.Inspection.Credential.Invalidate();
        if (tabs.SelectedIndex == 1)
            await workspace.Inspection.Credential.RefreshCommand.ExecuteAsync(default);
    }
}
