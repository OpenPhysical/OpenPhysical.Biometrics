// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class EnrollmentModuleTests
{
    [AvaloniaTest]
    public async Task SharedPersonAndRegisteredFingerprintSectionUseTheSameState()
    {
        await using var context = new EnrollmentOperatorContext(
            new ControlledExportStorage(),
            EnrollmentTestSuggestions.IndexPair()
        );
        var enrollment = context.Export.Enrollment;
        enrollment.Person.Name = "Sample person";
        enrollment.Person.Identifier = "sample-17";
        enrollment.Sections.Should().ContainSingle();
        var section = enrollment.Sections.Single();
        section.Id.Should().Be("fingerprints");
        section.Title.Should().Be("Fingerprints");
        section.ViewModel.Should().BeSameAs(enrollment.Fingerprints);
        section.Complete.Should().BeFalse();
        enrollment.Ready.Should().BeFalse();
    }

    [AvaloniaTest]
    public async Task WorkingSlotCanChangeWithoutLosingAcceptedCapturesOrExportOrder()
    {
        var storage = CompletedStorage();
        await using var context = new EnrollmentOperatorContext(
            storage,
            EnrollmentTestSuggestions.IndexPair()
        );
        var enrollment = context.Export.Enrollment;
        enrollment.Person.Name = "Sample person";
        await enrollment.Fingerprints.SecondarySlotCommand.ExecuteAsync(default);
        var left = StorageFixtures.Capture(FingerPosition.LeftIndex);
        await context.AcceptAsync(left);
        enrollment.Fingerprints.PrimaryActive.Should().BeTrue();
        var right = StorageFixtures.Capture(FingerPosition.RightIndex);
        await context.AcceptAsync(right);
        await enrollment.Fingerprints.SecondarySlotCommand.ExecuteAsync(default);
        enrollment.Fingerprints.ActiveAccepted.Should().BeTrue();
        enrollment.AcceptCommand.CanExecute(default).Should().BeFalse();
        await context.Export.SaveCommand.ExecuteAsync(default);
        storage.Accepted.Should().Equal(right, left);
    }

    [AvaloniaTest]
    public async Task RetryingOneAcceptedSlotPreservesTheOtherAndRejectsTheDiscardedSnapshot()
    {
        var storage = CompletedStorage();
        await using var context = new EnrollmentOperatorContext(
            storage,
            EnrollmentTestSuggestions.IndexPair()
        );
        var enrollment = context.Export.Enrollment;
        enrollment.Person.Name = "Sample person";
        var right = StorageFixtures.Capture(FingerPosition.RightIndex);
        var left = StorageFixtures.Capture(FingerPosition.LeftIndex);
        await context.AcceptAsync(right);
        await context.AcceptAsync(left);
        await enrollment.Fingerprints.PrimarySlotCommand.ExecuteAsync(default);
        await enrollment.Fingerprints.ResetCaptureCommand.ExecuteAsync(default);
        context.Export.Ready.Should().BeFalse();
        enrollment.Fingerprints.SecondaryLabel.Should().Be("Left Index accepted");
        await context.PresentAsync(right);
        enrollment.AcceptCommand.CanExecute(default).Should().BeFalse();
        var replacement = StorageFixtures.Capture(FingerPosition.RightIndex);
        await context.AcceptAsync(replacement);
        await context.Export.SaveCommand.ExecuteAsync(default);
        storage.Accepted.Should().Equal(replacement, left);
    }

    [AvaloniaTest]
    public async Task RegisteredSectionWritesAndReopensTheRealPackageForTheSharedPerson()
    {
        var storage = new TemplateStorage();
        var folder = ProcessingStorageFixtures.Folder();
        try
        {
            await using var context = new EnrollmentOperatorContext(
                storage,
                EnrollmentTestSuggestions.IndexPair()
            );
            context.Export.Folder = folder;
            context.Export.Enrollment.Person.Name = "Sample person";
            context.Export.Enrollment.Person.Identifier = "sample-17";
            context.Export.Enrollment.Fingerprints.TwoFingers = false;
            await context.AcceptAsync(context.NativeCapture());
            await context.Export.SaveCommand.ExecuteAsync(default);
            var path = Directory
                .EnumerateFiles(folder, "fingerprints.fmr", SearchOption.AllDirectories)
                .Single();
            var loaded = (await storage.LoadAsync(path, CancellationToken.None)).Value;
            loaded.Subjects.Should().Equal(new CapturePerson("Sample person", "sample-17"));
            loaded.Record.Views.Should().ContainSingle();
            context.Export.Status.Should().Contain("Saved:");
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    private static ControlledExportStorage CompletedStorage()
    {
        var storage = new ControlledExportStorage();
        storage.Completed.SetResult(Result.Success<string, DesktopError>("saved.fmr"));
        return storage;
    }
}
