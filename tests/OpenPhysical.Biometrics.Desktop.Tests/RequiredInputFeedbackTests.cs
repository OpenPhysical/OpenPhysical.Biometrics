// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

[TestFixture]
internal sealed class RequiredInputFeedbackTests
{
    [AvaloniaTest]
    public async Task MissingNameAndUnconfirmedReviewStayRedUntilCompletedIncludingWhileFocused()
    {
        using var host = DesktopServices.CreateHost();
        await using var context = new PersonWorkspaceContext();
        await context.PrepareFaceAsync();
        var window = ReviewWindow(host, context);
        window.Show();
        try
        {
            var name = window
                .GetVisualDescendants()
                .OfType<TextBox>()
                .Single(control => AutomationProperties.GetName(control) == "Name or label");
            var review = window
                .GetVisualDescendants()
                .OfType<CheckBox>()
                .Single(control =>
                    control.Content is TextBlock text
                    && text.Text == "I have reviewed the portrait, lighting, eyes and background."
                );
            review.Focus().Should().BeTrue();
            AssertRequiredAppearance(name, review);
            name.Focus().Should().BeTrue();
            AssertRequiredAppearance(name, review);
            WorkspaceReviewImages.Save(window, window.Bounds.Size, "required-inputs");
            name.Text = "Example person";
            review.IsChecked = true;
            NameOutline(name).Should().NotBe(Color.Parse("#B3261E"));
            ReviewOutline(review).Should().NotBe(Color.Parse("#B3261E"));
            name.Text = "  ";
            review.IsChecked = false;
            AssertRequiredAppearance(name, review);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window ReviewWindow(IHost host, PersonWorkspaceContext context) =>
        WorkspaceTestWindow.Create(
            host,
            new WorkspaceViewModel(
                context.Fingerprints.Capture,
                context.Export,
                context.Fingerprints.Inspection,
                host.Services.GetRequiredService<SettingsViewModel>()
            ),
            new Size(1100, 760)
        );

    private static bool AssertRequiredAppearance(TextBox name, CheckBox review)
    {
        AssertRed(NameOutline(name));
        AssertRed(ReviewOutline(review));
        name.Bounds.Height.Should().BeGreaterThanOrEqualTo(48);
        review.Bounds.Height.Should().BeGreaterThanOrEqualTo(48);
        return true;
    }

    private static Color NameOutline(TextBox name) =>
        (
            (ISolidColorBrush)
                name.GetVisualDescendants()
                    .OfType<Border>()
                    .First(border => border.Name == "PART_RootBorder")
                    .BorderBrush!
        ).Color;

    private static Color ReviewOutline(CheckBox review) =>
        (
            (ISolidColorBrush)
                review
                    .GetVisualDescendants()
                    .OfType<Path>()
                    .Single(path => path.Name == "Graphic")
                    .Fill!
        ).Color;

    private static bool AssertRed(Color color)
    {
        color.Should().Be(Color.Parse("#B3261E"));
        return true;
    }
}
