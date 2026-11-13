// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class ContactManifestSafetyTests
{
    [Test]
    public async Task OversizedManifestIsRejectedBeforeParsingOrLoadingCompanions()
    {
        var (folder, path, _) = await PackageTestFiles.SaveAsync(
            ProcessingStorageFixtures.Capture()
        );
        await File.WriteAllBytesAsync(
            Path.ChangeExtension(path, ".json"),
            new byte[256 * 1024 + 1]
        );
        (await new TemplateStorage().LoadAsync(path, CancellationToken.None))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Storage.Size");
        System.IO.Directory.Delete(folder, true);
    }
}
