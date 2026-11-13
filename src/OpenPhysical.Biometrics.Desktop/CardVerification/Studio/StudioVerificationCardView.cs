// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;

namespace OpenPhysical.Biometrics.Desktop;

internal static class StudioVerificationCardView
{
    internal static Control Create(StudioVerificationCardViewModel model)
    {
        var selections = Selections();
        selections.Bind(InputElement.IsEnabledProperty, new Binding(nameof(model.CanEdit)));
        var guidance = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                DesktopForms.Field(
                    "Enrolled finger",
                    DesktopForms.IndexChoice(nameof(model.Choices), nameof(model.FingerIndex))
                ),
                DesktopForms.Action("Use this finger", nameof(model.ContinueFingerCommand)),
            },
        };
        var next = DesktopForms.Action("Check card", nameof(model.PrimaryCommand));
        next.Name = "VerificationCheckCard";
        return new StackPanel
        {
            DataContext = model,
            Spacing = 12,
            Children =
            {
                selections,
                next,
                DesktopForms.Action("Cancel check", nameof(model.CancelCommand)),
                DesktopForms.Status(nameof(model.Status)),
                DesktopForms.Visible(guidance, nameof(model.AwaitingFinger)),
                new Expander
                {
                    Header = "Check details",
                    Content = DesktopForms.Status(nameof(model.ResultDetails)),
                },
                Details(model),
            },
        };
    }

    private static Control Selections() =>
        new StackPanel
        {
            Spacing = 12,
            Children =
            {
                DesktopForms.Field(
                    "Check",
                    DesktopForms.IndexChoice("Input.Choices", "Input.ChoiceIndex")
                ),
                DesktopForms.Field(
                    "Card reader",
                    DesktopForms.IndexChoice("Input.Readers", "Input.ReaderIndex", "DisplayName")
                ),
                DesktopForms.Visible(
                    DesktopForms.Field(
                        "Fingerprint scanner",
                        DesktopForms.IndexChoice(
                            "Input.Scanners",
                            "Input.ScannerIndex",
                            "DisplayName"
                        )
                    ),
                    "Input.NeedsScanner"
                ),
                DesktopForms.Visible(
                    DesktopForms.Field(
                        "Card PIN (sent once)",
                        DesktopForms.Input("Input.Pin", "Enter 6 to 8 digits", true)
                    ),
                    "Input.NeedsPin"
                ),
            },
        };

    private static Control Details(StudioVerificationCardViewModel model)
    {
        var content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Contactless(),
                DesktopForms.Visible(
                    DesktopForms.Field("MARSEC level", MarsecLevel()),
                    "Input.IsTwic"
                ),
                DesktopForms.Text(
                    "Certificates added here help build a path. They do not become trusted issuers."
                ),
                DesktopForms.Action("Add issuer certificate", nameof(model.ImportIssuerCommand)),
                DesktopForms.Action(
                    "Add content signer certificate",
                    nameof(model.ImportSignerCommand)
                ),
                DesktopForms.Visible(
                    DesktopForms.Action(
                        "Open TWIC privacy key",
                        nameof(model.ImportPrivacyKeyCommand)
                    ),
                    "Input.IsTwic"
                ),
                DesktopForms.Status(nameof(model.MaterialSummary)),
            },
        };
        content.Bind(InputElement.IsEnabledProperty, new Binding(nameof(model.CanEdit)));
        return new Expander { Header = "Additional card settings", Content = content };
    }

    private static Control Contactless()
    {
        var contactless = new CheckBox { Content = "Use the reader's contactless interface" };
        contactless.Bind(
            ToggleButton.IsCheckedProperty,
            new Binding("Input.Contactless") { Mode = BindingMode.TwoWay }
        );
        return contactless;
    }

    private static Control MarsecLevel()
    {
        var level = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 3,
            Increment = 1,
        };
        level.Bind(
            NumericUpDown.ValueProperty,
            new Binding("Input.MarsecLevel") { Mode = BindingMode.TwoWay }
        );
        return level;
    }
}
