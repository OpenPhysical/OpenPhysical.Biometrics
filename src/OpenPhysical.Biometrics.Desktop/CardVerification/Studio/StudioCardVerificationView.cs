// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Graphical card verification with progressive setup and explicit holder input.</summary>
public sealed class StudioCardVerificationView : UserControl
{
    /// <summary>Builds the live paired card-check workflow.</summary>
    /// <param name="model">Application-owned verification task.</param>
    public StudioCardVerificationView(StudioCardVerificationViewModel model)
    {
        DataContext = model;
        var checks = StudioVerificationCardView.Create(model.Card);
        checks.Bind(Visual.IsVisibleProperty, new Binding("Setup.Ready") { Source = model });
        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 18,
                MaxWidth = 760,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Check a card",
                        FontSize = 26,
                        FontWeight = FontWeight.SemiBold,
                    },
                    DesktopForms.Text(
                        "Check a presented card and, when required, its holder. This does not issue credentials or grant access."
                    ),
                    StudioVerificationSetupView.Create(model.Setup),
                    checks,
                },
            },
        };
    }
}
