// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class GuidedExportTests
{
    [AvaloniaTest]
    public async Task ClearPersonRestoresPlanAndMetadataWithoutKeepingAcceptedCaptures()
    {
        await using var context = new EnrollmentOperatorContext(
            new ControlledExportStorage(),
            EnrollmentTestSuggestions.IndexPair()
        );
        var model = context.Export;
        model.Enrollment.Person.Name = "Sample person";
        model.Enrollment.Person.Identifier = "sample-123";
        model.Enrollment.Fingerprints.PrimaryFinger = FingerChoice.For(FingerPosition.LeftThumb);
        model.Enrollment.Fingerprints.SecondaryFinger = FingerChoice.For(FingerPosition.LeftMiddle);
        model.ProductOwner = "0xFFFF";
        model.ProductType = "4";
        model.EquipmentId = "5";
        await context.AcceptAsync(StorageFixtures.Capture(FingerPosition.LeftThumb));
        await context.AcceptAsync(StorageFixtures.Capture(FingerPosition.LeftMiddle));
        model.Ready.Should().BeTrue();
        await model.ClearPersonCommand.ExecuteAsync(default);
        model.Enrollment.Person.Name.Should().BeEmpty();
        model.Enrollment.Person.Identifier.Should().BeEmpty();
        model.Enrollment.Fingerprints.PrimaryFinger.Position.Should().Be(FingerPosition.RightIndex);
        model
            .Enrollment.Fingerprints.SecondaryFinger.Position.Should()
            .Be(FingerPosition.LeftIndex);
        model.Enrollment.Progress.Should().Be("0 of 2 fingerprints accepted");
        model.ProductOwner.Should().Be("0x0103");
        model.ProductType.Should().Be("0");
        model.EquipmentId.Should().Be("0");
        model.Ready.Should().BeFalse();
    }

    [AvaloniaTest]
    public async Task PersonAndAcceptedPlanEnableOneSaveUsingTheDefaultProductOwner()
    {
        var storage = new ControlledExportStorage();
        await using var context = new EnrollmentOperatorContext(
            storage,
            EnrollmentTestSuggestions.IndexPair()
        );
        var model = context.Export;
        model.Ready.Should().BeFalse();
        model.Enrollment.Person.Name = "Sample person";
        model.Enrollment.Person.Identifier = "sample-123";
        await context.AcceptAsync(context.NativeCapture(FingerPosition.RightIndex));
        model.Enrollment.Progress.Should().Be("1 of 2 fingerprints accepted");
        model.Enrollment.Fingerprints.PrimaryLabel.Should().Contain("accepted");
        await context.AcceptAsync(context.NativeCapture(FingerPosition.LeftIndex));
        model.Ready.Should().BeTrue();
        var saving = model.SaveCommand.ExecuteAsync(default);
        await storage.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        storage.Record.Value.ProductOwner.Should().Be(0x0103);
        storage.Person.Value.Should().Be(new CapturePerson("Sample person", "sample-123"));
        model
            .Enrollment.Fingerprints.AcceptedCaptures.Select(value => value.Finger)
            .Should()
            .Equal(FingerPosition.RightIndex, FingerPosition.LeftIndex);
        storage.Completed.SetResult(Result.Success<string, DesktopError>("saved.opbio"));
        await saving;
    }
}
