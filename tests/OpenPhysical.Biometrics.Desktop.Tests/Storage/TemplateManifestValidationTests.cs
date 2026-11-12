// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text.Json.Nodes;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class TemplateManifestValidationTests
{
    [Test]
    public async Task CaseInsensitiveCompanionNamesCannotAliasDifferentViews()
    {
        var first = ProcessingStorageFixtures.Capture();
        var second = first with
        {
            Finger = FingerPosition.LeftIndex,
            View = first.View with { FingerPosition = (byte)FingerPosition.LeftIndex },
        };
        var record = UnsignedTemplateService.Create([first, second], new(1, 0, 0)).Value;
        var folder = ProcessingStorageFixtures.Folder();
        var path = (
            await new TemplateStorage().SaveAsync(
                record,
                [first, second],
                folder,
                CancellationToken.None
            )
        ).Value;
        var manifest = JsonNode.Parse(
            await File.ReadAllTextAsync(Path.ChangeExtension(path, ".json"))
        )!;
        manifest["Images"]![1]!["FileName"] =
            manifest["Images"]![0]!["FileName"]!.GetValue<string>()[..^4].ToUpperInvariant()
            + ".png";
        await PackageTestFiles.WriteAsync(path, manifest);
        (await new TemplateStorage().LoadAsync(path, CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Storage.Manifest");
        Directory.Delete(folder, true);
    }
}
