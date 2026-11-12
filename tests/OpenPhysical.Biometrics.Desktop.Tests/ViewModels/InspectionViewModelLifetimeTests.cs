// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class InspectionViewModelLifetimeTests
{
    [AvaloniaTest]
    public async Task ChangingAssignmentClearsTheCompletedDecisionAndOldScore()
    {
        var dependencies = new ControlledInspectionDependencies();
        dependencies.Release();
        var view = NewView(dependencies);
        await view.LoadCommand.ExecuteAsync(default);
        await view.CompareCommand.ExecuteAsync(default);
        view.Summary.Should().Be("Match");
        view.Report.Should().Contain("Bozorth3 score");
        view.AssignedProbeFinger = FingerChoice.All.Single(choice =>
            choice.Position == FingerPosition.LeftIndex
        );
        view.Summary.Should().Contain("Left Index");
        view.Report.Should().NotContain("Bozorth3 score");
        view.Report.Should().Contain("Compare");
        await view.StopAsync();
    }

    [AvaloniaTest]
    public async Task ReplacingTheReferenceCannotPublishAnOldComparisonBesideTheNewScene()
    {
        var dependencies = new ControlledInspectionDependencies();
        var view = NewView(dependencies);
        await view.LoadCommand.ExecuteAsync(default);
        var comparing = view.CompareCommand.ExecuteAsync(default);
        await dependencies.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        dependencies.Document = ControlledInspectionDependencies.DocumentFor(
            FingerPosition.LeftIndex
        );
        await view.LoadCommand.ExecuteAsync(default);
        var currentReport = view.Report;
        dependencies.Release();
        await comparing;
        view.Report.Should().Be(currentReport);
        await view.StopAsync();
    }

    [AvaloniaTest]
    public async Task ShutdownDrainsComparisonAndDisposesTheDisplayedDocument()
    {
        var dependencies = new ControlledInspectionDependencies();
        var view = NewView(dependencies);
        await view.LoadCommand.ExecuteAsync(default);
        var comparing = view.CompareCommand.ExecuteAsync(default);
        await dependencies.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var report = view.Report;
        var stopping = view.StopAsync();
        stopping.IsCompleted.Should().BeFalse();
        dependencies.Release();
        (await stopping).Should().Succeed();
        await comparing;
        view.Scene.HasNoValue.Should().BeTrue();
        view.Report.Should().Be(report);
        view.CompareCommand.CanExecute(default).Should().BeFalse();
    }

    private static InspectionViewModel NewView(ControlledInspectionDependencies dependencies) =>
        new(
            dependencies,
            new DesktopFilePicker(),
            dependencies,
            new TemplateComparison(new FingerprintComparisonService(dependencies)),
            new CredentialReaderService(
                new ScriptedCredentialTransport(new ScriptedCredentialCard())
            )
        );
}
