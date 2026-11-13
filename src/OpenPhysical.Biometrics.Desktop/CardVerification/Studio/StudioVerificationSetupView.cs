// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Controls;
using Avalonia.Data;

namespace OpenPhysical.Biometrics.Desktop;

internal static class StudioVerificationSetupView
{
    internal static Control Create(StudioVerificationSetupViewModel model)
    {
        var primary = DesktopForms.Action(string.Empty, nameof(model.PrimaryCommand));
        primary.Bind(ContentControl.ContentProperty, new Binding(nameof(model.PrimaryAction)));
        primary.Name = "VerificationSetupNext";
        var pairing = DesktopForms.Field(
            "Server address",
            DesktopForms.Input(nameof(model.ServerOrigin), "https://your-server.example")
        );
        var identities = DesktopForms.Field(
            "Sign in as",
            DesktopForms.IndexChoice(
                nameof(model.Operators),
                nameof(model.OperatorIndex),
                "DisplayName"
            )
        );
        return new StackPanel
        {
            DataContext = model,
            Spacing = 12,
            Children =
            {
                DesktopForms.Status(nameof(model.Status)),
                DesktopForms.Visible(pairing, nameof(model.ShowPairing)),
                DesktopForms.Visible(identities, nameof(model.ShowOperators)),
                DesktopForms.Visible(primary, nameof(model.ShowSetupAction)),
                Details(model),
                AdministratorSetup(),
            },
        };
    }

    private static Control Details(StudioVerificationSetupViewModel model) =>
        new Expander
        {
            Header = "Connection settings",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    DesktopForms.Action("Reload saved settings", nameof(model.RefreshCommand)),
                    DesktopForms.Action("Update signed settings", nameof(model.PrimaryCommand)),
                    DesktopForms.Action("Sign out", nameof(model.SignOutCommand)),
                    DesktopForms.Action("Disconnect this computer", nameof(model.UnpairCommand)),
                },
            },
        };

    private static Control AdministratorSetup() =>
        new Expander
        {
            Header = "Administrator setup",
            Content = DesktopForms.Text(
                "Card checks require a protected operator identity, current signed settings and an explicit trusted clock bound. "
                    + "Set OPENPHYSICAL_VERIFICATION_CLOCK_UNCERTAINTY_SECONDS to the administrator-approved bound (0 to 300 seconds), "
                    + "then restart Studio. Administrator guide: docs/CARD-VERIFICATION.md."
            ),
        };
}
