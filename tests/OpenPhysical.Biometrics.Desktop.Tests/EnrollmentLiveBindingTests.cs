// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class EnrollmentLiveBindingTests
{
    [AvaloniaTest]
    public async Task RepeatedLiveFramesAndSelectionWritebacksKeepOneCameraOpenAndItsModeSelected()
    {
        await using var context = new PersonWorkspaceContext();
        var window = CreateWindow(context);
        window.Show();
        (await context.Enrollment.ActivateAsync(true)).Should().Succeed();
        await FaceCaptureTestActions.ManualPreviewAsync(context.Face);
        var structure = new List<string>();
        context.Enrollment.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is "Plan" or "Sections" or "ActiveSection")
                structure.Add(args.PropertyName);
        };
        for (var index = 0; index < 100; index++)
        {
            var previous = context.Face.Image.Scene.Value;
            context.Camera.PublishNext().Should().Succeed();
            await WaitForFrameAsync(context.Face, previous);
            context.Person.Name = "Example person " + index;
            context.Enrollment.ActiveSection = context.Enrollment.ActiveSection;
            await context.Enrollment.SelectSectionCommand.ExecuteAsync(
                context.Enrollment.ActiveSection
            );
            context.Camera.OpenCount.Should().Be(1);
        }
        var mode = window
            .GetVisualDescendants()
            .OfType<ComboBox>()
            .Single(control => AutomationProperties.GetName(control) == "Native camera mode");
        mode.SelectedItem.Should().BeSameAs(context.Face.SelectedMode);
        structure.Should().BeEmpty();
        context.Face.IsLive.Should().BeTrue();
        context.Camera.StopCount.Should().Be(0);
        window.Close();
    }

    private static Window CreateWindow(PersonWorkspaceContext context)
    {
        var workspace = new WorkspaceViewModel(
            context.Fingerprints.Capture,
            context.Export,
            context.Fingerprints.Inspection,
            new SettingsViewModel(
                new SettingsStorage("/private/tmp/unused-binding-settings.json"),
                _ => Task.FromResult(UnitResult.Success<DesktopError>()),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsViewModel>.Instance
            )
        );
        return new Window
        {
            Content = new EnrollmentView { DataContext = workspace },
            Width = 1100,
            Height = 760,
        };
    }

    private static async Task WaitForFrameAsync(
        FaceCaptureViewModel face,
        FacePreviewScene previous
    )
    {
        for (
            var index = 0;
            index < 200 && ReferenceEquals(face.Image.Scene.Value, previous);
            index++
        )
        {
            await Task.Delay(5);
            Dispatcher.UIThread.RunJobs();
        }
        face.Image.Scene.Value.Should().NotBeSameAs(previous);
    }
}
