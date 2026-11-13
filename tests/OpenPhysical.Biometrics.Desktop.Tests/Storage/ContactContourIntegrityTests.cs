// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class ContactContourIntegrityTests
{
    [TestCase("Contact")]
    [TestCase("FilledHole")]
    [TestCase("Feature")]
    [TestCase("Support")]
    [TestCase("Context")]
    [TestCase("Uncertain")]
    [TestCase("GuardedContext")]
    [TestCase("ObservedContact")]
    public async Task EveryAdvertisedContourPlaneIsRequiredAndHashVerified(string role)
    {
        var (folder, path, manifest) = await PackageTestFiles.SaveAsync(
            HullStorageFixtures.Capture()
        );
        try
        {
            var descriptor = manifest["Images"]![0]!["Processing"]!["Contour"]!["Planes"]![role]!;
            var file = Path.Combine(
                Path.GetDirectoryName(path)!,
                descriptor["FileName"]!.GetValue<string>()
            );
            var bytes = await File.ReadAllBytesAsync(file);
            await File.WriteAllBytesAsync(file, [.. bytes, 0]);
            (await new TemplateStorage().LoadAsync(path, CancellationToken.None))
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be("Storage.Manifest");
            File.Delete(file);
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

    [TestCase("SourceDigest")]
    [TestCase("EvidencePolicyDigest")]
    [TestCase("PolicyDigest")]
    public async Task RehashedMetadataCannotContradictItsOriginalSourceOrPolicies(string field)
    {
        var (folder, path, manifest) = await PackageTestFiles.SaveAsync(
            HullStorageFixtures.Capture()
        );
        try
        {
            var processing = manifest["Images"]![0]!["Processing"]!;
            processing["Contour"]![field] = new string('0', 64);
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
    public async Task RehashedContextPixelsCannotBeRelabeledAsTheFeatureRegion()
    {
        var (folder, path, manifest) = await PackageTestFiles.SaveAsync(
            HullStorageFixtures.Capture()
        );
        try
        {
            var processing = manifest["Images"]![0]!["Processing"]!;
            var planes = processing["Contour"]!["Planes"]!;
            var feature = planes["Feature"]!;
            var context = planes["Context"]!;
            var directory = Path.GetDirectoryName(path)!;
            File.Copy(
                Path.Combine(directory, context["FileName"]!.GetValue<string>()),
                Path.Combine(directory, feature["FileName"]!.GetValue<string>()),
                true
            );
            feature["Sha256"] = context["Sha256"]!.DeepClone();
            feature["PixelSha256"] = context["PixelSha256"]!.DeepClone();
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
}
