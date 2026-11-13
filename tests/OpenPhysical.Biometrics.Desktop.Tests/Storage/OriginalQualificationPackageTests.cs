// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class OriginalQualificationPackageTests
{
    [Test]
    public async Task OfflinePackagesExplicitlyPreserveTheAbsenceOfPhysicalQualification()
    {
        var (folder, path, manifest) = await PackageTestFiles.SaveAsync(
            HullStorageFixtures.Capture()
        );
        try
        {
            var processing = manifest["Images"]![0]!["Processing"]!;
            processing.AsObject().ContainsKey("OriginalQualification").Should().BeTrue();
            processing["OriginalQualification"].Should().BeNull();
            var loaded = await new TemplateStorage().LoadAsync(path, CancellationToken.None);
            loaded.Should().Succeed();
            var evidence = loaded.Value.Images.Single().Processing.Value;
            evidence.InputRole.Should().Be(CaptureProcessingInputRole.DeclaredOfflineImage);
            evidence.OriginalQualification.HasNoValue.Should().BeTrue();
        }
        finally
        {
            System.IO.Directory.Delete(folder, true);
        }
    }

    [TestCase("LiveSensor")]
    [TestCase("Unknown")]
    public async Task MissingQualificationCannotBeRelabeledAsALiveCapture(string role)
    {
        var (folder, path, manifest) = await PackageTestFiles.SaveAsync(
            HullStorageFixtures.Capture()
        );
        try
        {
            var processing = manifest["Images"]![0]!["Processing"]!;
            processing["InputRole"] = role;
            processing["SettingsSha256"] = PackageTestFiles.SettingsDigest(processing);
            await PackageTestFiles.WriteAsync(path, manifest);
            (await new TemplateStorage().LoadAsync(path, CancellationToken.None))
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("Storage.Manifest");
        }
        finally
        {
            System.IO.Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task SavingLiveSensorEvidenceWithoutItsOriginalQualificationFailsBeforeWriting()
    {
        var capture = HullStorageFixtures.Capture();
        capture = capture with
        {
            Processing = capture.Processing.Value with
            {
                InputRole = CaptureProcessingInputRole.LiveSensor,
            },
        };
        var folder = ProcessingStorageFixtures.Folder();
        var saved = await new TemplateStorage().SaveAsync(
            UnsignedTemplateService.Create([capture], new(1, 0, 0)).Value,
            [capture],
            folder,
            CancellationToken.None
        );
        saved.Should().Fail().Which.Code.Should().Be("Storage.Manifest");
        System.IO.Directory.Exists(folder).Should().BeFalse();
    }
}
