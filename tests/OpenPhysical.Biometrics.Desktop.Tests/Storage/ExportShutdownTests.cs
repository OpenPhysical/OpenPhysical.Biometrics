// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class ExportShutdownTests
{
    [AvaloniaTest]
    public async Task StopCancelsAndDrainsTheSaveWithoutUpdatingClosedViewState()
    {
        var storage = new ControlledExportStorage();
        await using var context = new EnrollmentOperatorContext(
            storage,
            EnrollmentTestSuggestions.IndexPair()
        );
        var model = context.Export;
        model.Enrollment.Person.Name = "Sample person";
        model.Enrollment.Fingerprints.TwoFingers = false;
        await context.AcceptAsync(StorageFixtures.Capture());
        var save = model.SaveCommand.ExecuteAsync(default);
        await storage.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var status = model.Status;
        var stopped = model.StopAsync();
        stopped.IsCompleted.Should().BeFalse();
        storage.Token.IsCancellationRequested.Should().BeTrue();
        model.SaveCommand.CanExecute(default).Should().BeFalse();
        storage.Completed.SetResult(Result.Success<string, DesktopError>("saved.fmr"));
        (await stopped.WaitAsync(TimeSpan.FromSeconds(2))).Should().Succeed();
        await save;
        model.Status.Should().Be(status);
        await model.SaveCommand.ExecuteAsync(default);
        storage.Saves.Should().Be(1);
    }

    [AvaloniaTest]
    public async Task StoppedIdleExportRejectsAcceptAndClearWithoutPropertyChanges()
    {
        await using var context = new EnrollmentOperatorContext(
            new ControlledExportStorage(),
            EnrollmentTestSuggestions.IndexPair()
        );
        await context.PresentAsync(StorageFixtures.Capture());
        var model = context.Export;
        (await model.StopAsync()).Should().Succeed();
        var changes = 0;
        model.PropertyChanged += (_, _) => changes++;
        await model.Enrollment.AcceptCommand.ExecuteAsync(default);
        await model.ClearPersonCommand.ExecuteAsync(default);
        model.Enrollment.AcceptCommand.CanExecute(default).Should().BeFalse();
        model.ClearPersonCommand.CanExecute(default).Should().BeFalse();
        changes.Should().Be(0);
    }
}
