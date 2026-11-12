// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Platform;
using AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class MaterialControlLayoutTests
{
    [AvaloniaTest]
    public void MaterialControlsUseBundledRobotoAndInteractiveBoundsAreAtLeast48Dip()
    {
        var controls = Controls();
        var panel = new StackPanel { Spacing = 8 };
        foreach (var control in controls)
            panel.Children.Add(control);
        var window = new Window
        {
            Content = panel,
            Width = 420,
            Height = 620,
        };
        window.Show();
        try
        {
            foreach (var control in controls)
                AssertRoboto(control);
            foreach (var control in controls.Take(4))
            {
                control.Bounds.Width.Should().BeGreaterThanOrEqualTo(48);
                control.Bounds.Height.Should().BeGreaterThanOrEqualTo(48);
            }
            AssetLoader
                .Exists(new Uri("avares://Material.Styles/Fonts/Roboto/Roboto-Regular.ttf"))
                .Should()
                .BeTrue();
            AssetLoader
                .Exists(new Uri("avares://Material.Styles/Fonts/Roboto/Roboto-Medium.ttf"))
                .Should()
                .BeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    private static FontFamily AssertRoboto(Control control)
    {
        var font = control.GetValue(TextElement.FontFamilyProperty);
        font.Name.Should().Be("Roboto");
        font.Key.Should().NotBeNull();
        new Typeface(font).GlyphTypeface.FamilyName.Should().StartWith("Roboto");
        new Typeface(font, FontStyle.Normal, FontWeight.Medium)
            .GlyphTypeface.Weight.Should()
            .Be(FontWeight.Medium);
        return font;
    }

    private static ImmutableArray<Control> Controls() =>
        [
            new Button { Content = "Save", Classes = { "compact" } },
            new TextBox { Watermark = "Name or label" },
            new ComboBox { ItemsSource = ImmutableArray.Create("First finger"), SelectedIndex = 0 },
            new CheckBox { Content = "Auto capture" },
            new TextBlock { Text = "Heading" },
            new ToolTip { Content = "Zoom in" },
            new FlyoutPresenter { Content = "Settings" },
        ];
}
