// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text.Json.Nodes;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class ProcessingPackageTests
{
    [Test]
    public async Task ReopenDefaultsToVerifiedProcessingPixelsAndRetainsOriginalEvidence()
    {
        var capture = ProcessingStorageFixtures.Capture();
        var artifact = capture.Processing.Value.Artifact;
        var record = UnsignedTemplateService.Create([capture], new(1, 0, 0)).Value;
        var folder = ProcessingStorageFixtures.Folder();
        var store = new TemplateStorage();
        var saved = await store.SaveAsync(record, [capture], folder, CancellationToken.None);
        saved.Should().Succeed();
        (await File.ReadAllBytesAsync(saved.Value))
            .Should()
            .Equal(Incits378Codec.Write(record).Value);
        var loaded = await store.LoadAsync(saved.Value, CancellationToken.None);
        loaded.Should().Succeed();
        var image = loaded.Value.Images.Single();
        image.GrayPixels.Should().Equal(artifact.Pixels).And.NotEqual(capture.Frame.Pixels);
        image.CapturedAtUtc.Should().Be(capture.Frame.CapturedAt);
        image.Processing.HasValue.Should().BeTrue();
        var evidence = image.Processing.Value;
        evidence.Original.GrayPixels.Should().Equal(capture.Frame.Pixels);
        evidence.OriginalAssessment.Nfiq2.Value.Score.Should().Be(60);
        evidence.ProcessedAssessment.Nfiq2.Value.Score.Should().Be(73);
        evidence.Policy.Contrast.Should().Be(30);
        evidence.SelectionPolicy.MinimumNfiq1Gain.Should().Be(2);
        evidence.SelectionPolicy.MinimumNfiq2Gain.Should().Be(7);
        evidence.ProcessingRevision.Should().Be(4);
        evidence.Identity.Should().Be(artifact.Identity);
        evidence.Mask.Digest.Should().Be(artifact.Mask.Digest);
        evidence.Candidates.Single().Motion.HasNoValue.Should().BeTrue();
        var manifest = JsonNode.Parse(
            await File.ReadAllBytesAsync(Path.ChangeExtension(saved.Value, ".json"))
        )!;
        _ = PackageTestFiles.AssertAdvertisedPngs(PrivateCaptureDirectory(saved.Value), manifest);
        Directory.Delete(folder, true);
    }

    [TestCase("Original")]
    [TestCase("Inner")]
    public async Task AnAdvertisedMissingProcessingCompanionFailsClosed(string role)
    {
        var (folder, path, manifest) = await SaveAsync();
        var processing = manifest["Images"]![0]!["Processing"]!;
        var descriptor = role == "Original" ? processing["Original"]! : processing["Masks"]![role]!;
        File.Delete(
            Path.Combine(Path.GetDirectoryName(path)!, descriptor["FileName"]!.GetValue<string>())
        );
        (await new TemplateStorage().LoadAsync(path, CancellationToken.None)).Should().Fail();
        Directory.Delete(folder, true);
    }

    [Test]
    public async Task ProcessingPolicyTamperingCannotReuseTheSavedSettingsIdentity()
    {
        var (folder, path, manifest) = await SaveAsync();
        manifest["Images"]![0]!["Processing"]!["Policy"]!["Contrast"] = 31;
        await File.WriteAllTextAsync(Path.ChangeExtension(path, ".json"), manifest.ToJsonString());
        (await new TemplateStorage().LoadAsync(path, CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Storage.Manifest");
        Directory.Delete(folder, true);
    }

    [Test]
    public async Task ProcessingSourceCannotContradictTheDeclaredAcquisitionExposure()
    {
        var (folder, path, manifest) = await SaveAsync();
        manifest["Images"]![0]!["Provenance"]!["Settings"]!["Exposure"] = 42;
        await File.WriteAllTextAsync(Path.ChangeExtension(path, ".json"), manifest.ToJsonString());
        (await new TemplateStorage().LoadAsync(path, CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Storage.Manifest");
        Directory.Delete(folder, true);
    }

    private static string PrivateCaptureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(path).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.GetUnixFileMode(directory)
                .Should()
                .Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        return directory;
    }

    private static async Task<(string Folder, string Path, JsonNode Manifest)> SaveAsync()
    {
        var capture = ProcessingStorageFixtures.Capture();
        var record = UnsignedTemplateService.Create([capture], new(1, 0, 0)).Value;
        var folder = ProcessingStorageFixtures.Folder();
        var saved = (
            await new TemplateStorage().SaveAsync(record, [capture], folder, CancellationToken.None)
        ).Value;
        var manifest = JsonNode.Parse(
            await File.ReadAllBytesAsync(Path.ChangeExtension(saved, ".json"))
        )!;
        return (folder, saved, manifest);
    }
}
