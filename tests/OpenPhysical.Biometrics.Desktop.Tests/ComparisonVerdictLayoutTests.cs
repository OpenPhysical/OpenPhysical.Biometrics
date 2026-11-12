// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class ComparisonVerdictLayoutTests
{
    [AvaloniaTest]
    public async Task CurrentDecisionsUseLargeDistinctColorsAndDisappearWhenTheCaptureIsCleared()
    {
        using var host = DesktopServices.CreateHost();
        var dependencies = new ControlledInspectionDependencies();
        dependencies.Release();
        var inspection = new InspectionViewModel(
            dependencies,
            new DesktopFilePicker(),
            dependencies,
            new TemplateComparison(new FingerprintComparisonService(dependencies)),
            new CredentialReaderService(
                new ScriptedCredentialTransport(new ScriptedCredentialCard())
            )
        );
        var standard = host.Services.GetRequiredService<WorkspaceViewModel>();
        var view = CreateView(standard, inspection);
        var window = ShowView(view);
        await inspection.LoadCommand.ExecuteAsync(default);
        await inspection.CompareCommand.ExecuteAsync(default);
        var verdict = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(text => text.Classes.Contains("verdict"));
        window.UpdateLayout();
        AssertVerdict(verdict, "Match", "#176C3A");
        WorkspaceReviewImages.Save(window, new Size(800, 600), "match");
        inspection.Threshold = 43;
        verdict.IsVisible.Should().BeFalse();
        await inspection.CompareCommand.ExecuteAsync(default);
        window.UpdateLayout();
        AssertVerdict(verdict, "No match", "#B3261E");
        WorkspaceReviewImages.Save(window, new Size(800, 600), "no-match");
        dependencies.SetCapture(Maybe<CapturedFingerprint>.None);
        verdict.IsVisible.Should().BeFalse();
        await inspection.StopAsync();
        window.Close();
    }

    private static Window ShowView(VerificationView view)
    {
        var window = new Window
        {
            Content = view,
            Width = 800,
            Height = 600,
        };
        window.Show();
        return window;
    }

    private static VerificationView CreateView(
        WorkspaceViewModel standard,
        InspectionViewModel inspection
    ) =>
        new()
        {
            DataContext = new WorkspaceViewModel(
                standard.Capture,
                standard.Export,
                inspection,
                standard.Settings
            ),
        };

    private static bool AssertVerdict(TextBlock verdict, string expected, string color)
    {
        verdict.IsEffectivelyVisible.Should().BeTrue();
        verdict.Text.Should().Be(expected);
        verdict.FontSize.Should().Be(24);
        verdict.Bounds.Height.Should().BeGreaterThanOrEqualTo(24);
        ((ISolidColorBrush)verdict.Foreground!).Color.Should().Be(Color.Parse(color));
        return true;
    }
}
