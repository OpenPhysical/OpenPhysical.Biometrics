// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class ReviewWorkspaceTests
{
    [AvaloniaTest]
    public async Task PendingAndAcceptedReviewsKeepOnePrimaryActionAndTheSamePreparedImage()
    {
        var host = DesktopServices.CreateHost();
        var workspace = host.Services.GetRequiredService<WorkspaceViewModel>();
        var enrollment = workspace.Export.Enrollment;
        enrollment.Person.Name = "Sample person";
        enrollment.Fingerprints.PrimaryFinger = FingerChoice.For(FingerPosition.RightIndex);
        enrollment.Fingerprints.SecondaryFinger = FingerChoice.For(FingerPosition.LeftIndex);
        var prepared = HullStorageFixtures.Capture(modified: true);
        var frame = prepared.Frame;
        var pixels = prepared.Processing.Value.Artifact.Pixels;
        (await workspace.Capture.ReviewAcceptedAsync(prepared)).Should().Succeed();
        workspace.Capture.Scene.Value.GrayPixels.Should().Equal(pixels).And.NotEqual(frame.Pixels);
        enrollment.Fingerprints.PrimaryLabel.Should().Be("Waiting for acceptance");
        AssertReviewSizes(host, workspace, "pending", "Accept finger");
        enrollment.Fingerprints.TwoFingers = false;
        await enrollment.AcceptCommand.ExecuteAsync(default);
        enrollment.Ready.Should().BeTrue();
        workspace.Capture.Scene.Value.GrayPixels.Should().Equal(pixels);
        AssertReviewSizes(host, workspace, "accepted", "Save person");
        workspace.Capture.Captured.Value.Should().BeSameAs(prepared);
        workspace.Capture.Captured.Value.Frame.Should().BeSameAs(frame);
        await workspace.Capture.StopAsync();
        await workspace.Inspection.StopAsync();
        await ((IAsyncDisposable)host).DisposeAsync();
    }

    private static bool AssertReviewSizes(
        IHost host,
        WorkspaceViewModel workspace,
        string label,
        string action
    )
    {
        foreach (var size in new[] { new Size(800, 600), new Size(1100, 760), new Size(1280, 880) })
        {
            var window = WorkspaceTestWindow.Create(host, workspace, size);
            var view = (Control)window.Content!;
            window.Show();
            WorkspaceReviewImages.Save(window, size, label);
            var primary = view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button =>
                    button.IsEffectivelyVisible
                    && button.Content is string text
                    && text is "Capture now" or "Accept finger" or "Save person"
                )
                .ToArray();
            primary.Should().ContainSingle().Which.Content.Should().Be(action);
            primary[0].IsEnabled.Should().BeTrue();
            var position = primary[0].TranslatePoint(default, view)!.Value;
            (position.Y + primary[0].Bounds.Height)
                .Should()
                .BeLessThanOrEqualTo(view.Bounds.Height);
            primary[0].Bounds.Height.Should().BeGreaterThanOrEqualTo(48);
            window.Close();
        }
        return true;
    }
}
