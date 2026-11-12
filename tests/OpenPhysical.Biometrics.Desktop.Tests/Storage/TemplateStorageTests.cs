// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;

[TestFixture]
internal sealed class TemplateStorageTests
{
    [Test]
    public async Task StandaloneFmrLoadsWithoutInventingAnImage()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".fmr");
        var record = UnsignedTemplateService
            .Create([ProcessingStorageFixtures.Capture()], new TemplateMetadata(1, 0, 0))
            .Value;
        await File.WriteAllBytesAsync(path, Incits378Codec.Write(record).Value);
        var loaded = await new TemplateStorage().LoadAsync(path, CancellationToken.None);
        loaded.Should().Succeed();
        loaded.Value.Images.Should().BeEmpty();
        File.Delete(path);
    }

    [Test]
    public async Task ModifiedSourceImageFailsClosed()
    {
        var capture = ProcessingStorageFixtures.Capture();
        var folder = Path.Combine(
            Path.GetTempPath(),
            "biometrics-tamper-" + Guid.NewGuid().ToString("N")
        );
        var record = UnsignedTemplateService.Create([capture], new TemplateMetadata(1, 0, 0)).Value;
        var store = new TemplateStorage();
        var saved = (
            await store.SaveAsync(record, [capture], folder, CancellationToken.None)
        ).Value;
        var image = Directory.GetFiles(Path.GetDirectoryName(saved)!, "*-processing.png").Single();
        await File.WriteAllBytesAsync(image, [1, 2, 3]);
        (await store.LoadAsync(saved, CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Storage.Manifest");
        Directory.Delete(folder, true);
    }

    [Test]
    public async Task ManifestCannotReferenceAFileOutsideItsCaptureFolder()
    {
        var capture = ProcessingStorageFixtures.Capture();
        var folder = Path.Combine(
            Path.GetTempPath(),
            "biometrics-path-" + Guid.NewGuid().ToString("N")
        );
        var record = UnsignedTemplateService.Create([capture], new TemplateMetadata(1, 0, 0)).Value;
        var store = new TemplateStorage();
        var saved = (
            await store.SaveAsync(record, [capture], folder, CancellationToken.None)
        ).Value;
        var manifest = Path.ChangeExtension(saved, ".json");
        var json = JsonNode.Parse(await File.ReadAllTextAsync(manifest))!;
        json["Images"]![0]!["FileName"] = "../outside.png";
        await File.WriteAllTextAsync(manifest, json.ToJsonString());
        (await store.LoadAsync(saved, CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Storage.Manifest");
        Directory.Delete(folder, true);
    }

    [Test]
    public async Task AChangedValidTemplateCannotReuseOldImageAssociations()
    {
        var capture = ProcessingStorageFixtures.Capture();
        var folder = Path.Combine(
            Path.GetTempPath(),
            "biometrics-record-" + Guid.NewGuid().ToString("N")
        );
        var record = UnsignedTemplateService.Create([capture], new TemplateMetadata(1, 0, 0)).Value;
        var store = new TemplateStorage();
        var saved = (
            await store.SaveAsync(record, [capture], folder, CancellationToken.None)
        ).Value;
        var bytes = await File.ReadAllBytesAsync(saved);
        bytes[13] ^= 1;
        Incits378Codec.Parse(bytes).Should().Succeed();
        await File.WriteAllBytesAsync(saved, bytes);
        (await store.LoadAsync(saved, CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Storage.Manifest");
        Directory.Delete(folder, true);
    }

    [Test]
    public async Task ExportRejectsViewsFromADifferentCapture()
    {
        var capture = ProcessingStorageFixtures.Capture();
        var folder = Path.Combine(
            Path.GetTempPath(),
            "biometrics-association-" + Guid.NewGuid().ToString("N")
        );
        var record = UnsignedTemplateService.Create([capture], new TemplateMetadata(1, 0, 0)).Value;
        var replacement = capture with
        {
            View = capture.View with { Minutiae = [capture.View.Minutiae[0] with { X = 4 }] },
        };
        (
            await new TemplateStorage().SaveAsync(
                record,
                [replacement],
                folder,
                CancellationToken.None
            )
        )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Storage.Association");
        Directory.Exists(folder).Should().BeFalse();
    }
}
