// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class CurrentCapturePackageTests
{
    [Test]
    public async Task AStudioPackageRequiresPreparedEvidenceBeforeAnyFilesAreWritten()
    {
        var capture = StorageFixtures.Capture();
        var folder = ProcessingStorageFixtures.Folder();
        var result = await new TemplateStorage().SaveAsync(
            UnsignedTemplateService.Create([capture], new(1, 0, 0)).Value,
            [capture],
            folder,
            CancellationToken.None
        );
        result.Should().Fail().Which.Code.Should().Be("Storage.Manifest");
        Directory.Exists(folder).Should().BeFalse();
    }

    [Test]
    public async Task AStudioPackageRequiresItsFullAcquisitionSnapshot()
    {
        var capture = ProcessingStorageFixtures.Capture();
        capture = capture with
        {
            Processing = capture.Processing.Value with { OriginalAcquisitionPolicy = default },
        };
        var folder = ProcessingStorageFixtures.Folder();
        var result = await new TemplateStorage().SaveAsync(
            UnsignedTemplateService.Create([capture], new(1, 0, 0)).Value,
            [capture],
            folder,
            CancellationToken.None
        );
        result.Should().Fail().Which.Code.Should().Be("Storage.Manifest");
        Directory.Exists(folder).Should().BeFalse();
    }

    [TestCase("Processing")]
    [TestCase("Contour")]
    [TestCase("OriginalAcquisitionPolicy")]
    [TestCase("InputRole")]
    [TestCase("OriginalQualification")]
    public async Task RemovingRequiredCompanionMetadataCannotTurnAPackageIntoAPlainImport(
        string field
    )
    {
        var (folder, path, manifest) = await PackageTestFiles.SaveAsync(
            ProcessingStorageFixtures.Capture()
        );
        if (field == "Processing")
            manifest["Images"]![0]!.AsObject().Remove(field);
        else
        {
            var processing = manifest["Images"]![0]!["Processing"]!;
            processing.AsObject().Remove(field);
            processing["SettingsSha256"] = PackageTestFiles.SettingsDigest(processing);
        }
        await PackageTestFiles.WriteAsync(path, manifest);
        (await new TemplateStorage().LoadAsync(path, CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Storage.Manifest");
        Directory.Delete(folder, true);
    }

    [Test]
    public async Task UnsupportedPackageMarkerIsRejectedWithoutMigration()
    {
        var (folder, path, manifest) = await PackageTestFiles.SaveAsync(
            ProcessingStorageFixtures.Capture()
        );
        manifest["Version"] = 1;
        await PackageTestFiles.WriteAsync(path, manifest);
        (await new TemplateStorage().LoadAsync(path, CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Storage.Manifest");
        Directory.Delete(folder, true);
    }

    [Test]
    public async Task DiagnosticOptInPreservesEveryStageAndRequiresAdvertisedCompanions()
    {
        var capture = ProcessingStorageFixtures.Capture();
        var (folder, path, manifest) = await PackageTestFiles.SaveAsync(capture, new(true));
        var loaded = await new TemplateStorage().LoadAsync(path, CancellationToken.None);
        loaded.Should().Succeed();
        var contact = loaded.Value.Images.Single().Processing.Value.ContactDiagnostics.Value;
        var expected = capture.Processing.Value.ContactDiagnostics;
        contact.CandidateMask.Value.Should().Equal(expected.CandidateMask);
        contact.RidgeSupportMask.Value.Should().Equal(expected.RidgeSupportMask);
        contact.AcceptedMask.Value.Should().Equal(expected.AcceptedMask);
        contact.RejectedMask.Value.Should().Equal(expected.RejectedMask);
        contact.InnerMask.Value.Should().Equal(expected.InnerMask);
        contact.SourceDigest.Should().Be(expected.SourceDigest);
        contact.PolicyDigest.Should().Be(expected.PolicyDigest);
        _ = PackageTestFiles.AssertAdvertisedPngs(Path.GetDirectoryName(path)!, manifest);
        var name = manifest["Images"]![0]!["Processing"]!["ContactDiagnostics"]!["Planes"]![
            "Candidate"
        ]!["FileName"]!.GetValue<string>();
        File.Delete(Path.Combine(Path.GetDirectoryName(path)!, name));
        (await new TemplateStorage().LoadAsync(path, CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Storage.Manifest");
        Directory.Delete(folder, true);
    }
}
