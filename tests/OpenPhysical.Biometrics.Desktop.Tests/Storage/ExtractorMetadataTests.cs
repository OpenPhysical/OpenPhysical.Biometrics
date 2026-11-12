// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class ExtractorMetadataTests
{
    [Test]
    public async Task ExactExtractorIdentitySurvivesCurrentPackageRoundtrip()
    {
        var capture = ProcessingStorageFixtures.Capture() with
        {
            ExtractorVersion = "NBIS 5.0.0 MINDTCT V2",
        };
        var (folder, path, _) = await PackageTestFiles.SaveAsync(capture);
        var loaded = await new TemplateStorage().LoadAsync(path, CancellationToken.None);
        loaded.Should().Succeed();
        loaded.Value.Images.Single().ExtractorVersion.Value.Should().Be("NBIS 5.0.0 MINDTCT V2");
        Directory.Delete(folder, true);
    }

    [Test]
    public async Task MalformedAdvertisedExtractorIdentityFailsOpen()
    {
        var (folder, path, manifest) = await PackageTestFiles.SaveAsync(
            ProcessingStorageFixtures.Capture()
        );
        manifest["Images"]![0]!["ExtractorVersion"] = " ";
        await PackageTestFiles.WriteAsync(path, manifest);
        (await new TemplateStorage().LoadAsync(path, CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Storage.Manifest");
        Directory.Delete(folder, true);
    }
}
