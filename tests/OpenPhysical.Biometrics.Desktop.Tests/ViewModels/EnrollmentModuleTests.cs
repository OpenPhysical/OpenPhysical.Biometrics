// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text.Json;
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.Packages;

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
        var left = context.NativeCapture(FingerPosition.LeftIndex);
        await context.AcceptAsync(left);
        enrollment.Fingerprints.PrimaryActive.Should().BeTrue();
        var right = context.NativeCapture(FingerPosition.RightIndex);
        await context.AcceptAsync(right);
        await enrollment.Fingerprints.SecondarySlotCommand.ExecuteAsync(default);
        enrollment.Fingerprints.ActiveAccepted.Should().BeTrue();
        enrollment.AcceptCommand.CanExecute(default).Should().BeFalse();
        await context.Export.SaveCommand.ExecuteAsync(default);
        enrollment.Fingerprints.AcceptedCaptures.Should().Equal(right, left);
        storage
            .Record.Value.Views.Select(view => view.FingerPosition)
            .Should()
            .Equal((byte)FingerPosition.RightIndex, (byte)FingerPosition.LeftIndex);
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
        var right = context.NativeCapture(FingerPosition.RightIndex);
        var left = context.NativeCapture(FingerPosition.LeftIndex);
        await context.AcceptAsync(right);
        await context.AcceptAsync(left);
        await enrollment.Fingerprints.PrimarySlotCommand.ExecuteAsync(default);
        await enrollment.Fingerprints.ResetCaptureCommand.ExecuteAsync(default);
        context.Export.Ready.Should().BeFalse();
        enrollment.Fingerprints.SecondaryLabel.Should().Be("Left Index accepted");
        await context.PresentAsync(right);
        enrollment.AcceptCommand.CanExecute(default).Should().BeFalse();
        var replacement = context.NativeCapture(FingerPosition.RightIndex);
        await context.AcceptAsync(replacement);
        await context.Export.SaveCommand.ExecuteAsync(default);
        enrollment.Fingerprints.AcceptedCaptures.Should().Equal(replacement, left);
        storage.Saves.Should().Be(1);
    }

    [AvaloniaTest]
    public async Task RegisteredSectionWritesAndReopensTheRealPackageForTheSharedPerson()
    {
        var storage = new EnrollmentPackageStorage();
        var folder = EnrollmentExportFixtures.Folder();
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
            var capture = context.NativeCapture();
            await context.AcceptAsync(capture);
            var counts = context.Counts;
            await context.Export.SaveCommand.ExecuteAsync(default);
            context.Export.Status.Should().Contain("Saved:");
            var path = Directory.EnumerateFiles(folder, "*.opbio").Single();
            Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Should().Equal(path);
            using var secret = EnrollmentExportFixtures.Secret();
            var loaded = await storage.OpenAsync(path, secret, CancellationToken.None);
            loaded.Should().Succeed();
            CheckPackage(loaded.Value, capture);
            context.Counts.Should().Be(counts);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    private static void CheckPackage(BiometricPackage package, CapturedFingerprint capture)
    {
        package.Label.Should().Be("Sample person");
        package.ExternalId.Value.Should().Be("sample-17");
        var record = UnsignedTemplateService
            .Create([capture], new TemplateMetadata(0x0103, 0, 0))
            .Value;
        package
            .Artifacts.Single(artifact => artifact.Name == "fingerprints.fmr")
            .Bytes.Should()
            .Equal(Incits378Codec.Write(record).Value);
        using var evidence = JsonDocument.Parse(package.EvidenceJson.ToArray());
        evidence
            .RootElement.GetProperty("Purpose")
            .GetString()
            .Should()
            .Be("unsigned-issuance-staging");
        evidence.RootElement.GetProperty("Sections")[0].GetString().Should().Be("fingerprints");
        using var manifest = JsonDocument.Parse(
            package
                .Artifacts.Single(artifact => artifact.Name == "fingerprints.json")
                .Bytes.ToArray()
        );
        var image = manifest.RootElement.GetProperty("Images")[0];
        CheckImage(package, image);
        CheckImage(package, image.GetProperty("Processing").GetProperty("Original"));
    }

    private static void CheckImage(BiometricPackage package, JsonElement descriptor)
    {
        var artifact = package.Artifacts.Single(value =>
            value.Name == descriptor.GetProperty("FileName").GetString()
        );
        artifact.Sha256.Should().BeEquivalentTo(descriptor.GetProperty("Sha256").GetString());
        artifact
            .Bytes.AsSpan()
            .StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            .Should()
            .BeTrue();
    }

    private static ControlledExportStorage CompletedStorage()
    {
        var storage = new ControlledExportStorage();
        storage.Completed.SetResult(Result.Success<string, DesktopError>("saved.opbio"));
        return storage;
    }
}
