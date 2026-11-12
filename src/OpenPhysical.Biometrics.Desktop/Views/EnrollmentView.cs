// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

/// <summary>Person enrollment and fingerprint capture workspace.</summary>
public sealed class EnrollmentView : UserControl
{
    /// <summary>Creates the enrollment workspace.</summary>
    public EnrollmentView() => AvaloniaXamlLoader.Load(this);

    private void DetailsOpened(object sender, EventArgs args)
    {
        if (DataContext is WorkspaceViewModel workspace)
        {
            var result = workspace.Capture.ProcessingDetails.SetOpen(true);
            if (result.IsFailure)
                workspace.Capture.Report(result.Error);
        }
    }

    private void DetailsClosed(object sender, EventArgs args)
    {
        if (DataContext is WorkspaceViewModel workspace)
            workspace.Capture.ProcessingDetails.SetOpen(false);
    }
}
