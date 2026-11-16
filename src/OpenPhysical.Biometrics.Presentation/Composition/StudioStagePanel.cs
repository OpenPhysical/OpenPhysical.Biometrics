// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Controls;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Shared heading and task-body layout using the Studio theme.</summary>
public sealed class StudioStagePanel : UserControl
{
    /// <summary>Wraps an application task in the shared stage spacing and typography.</summary>
    /// <param name="title">Visible task heading.</param>
    /// <param name="body">Application-owned task controls.</param>
    public StudioStagePanel(string title, Control body)
    {
        var heading = new TextBlock { Text = title };
        heading.Classes.Add("section");
        var panel = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 16 };
        panel.Children.Add(heading);
        Grid.SetRow(body, 1);
        panel.Children.Add(body);
        Content = panel;
    }
}
