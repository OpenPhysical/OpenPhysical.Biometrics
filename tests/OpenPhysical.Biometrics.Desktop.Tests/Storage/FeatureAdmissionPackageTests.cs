// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Fingerprints;

[TestFixture]
internal sealed class FeatureAdmissionPackageTests
{
    [Test]
    public async Task SavingAContextOnlyPointFailsBeforeWritingAPackage()
    {
        var capture = HullStorageFixtures.Capture();
        capture = capture with { View = ContextOnlyView(capture) };
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

    [Test]
    public async Task RehashedTemplateCannotAuthorizeAPointFromProcessingContext()
    {
        var capture = HullStorageFixtures.Capture();
        var (folder, path, manifest) = await PackageTestFiles.SaveAsync(capture);
        try
        {
            var record = UnsignedTemplateService.Create([capture], new(1, 0, 0)).Value;
            var bytes = Incits378Codec
                .Write(record with { Views = [ContextOnlyView(capture)] })
                .Value;
            await File.WriteAllBytesAsync(path, bytes);
            manifest["TemplateSha256"] = PackageTestFiles.Digest(bytes);
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

    private static FingerprintView ContextOnlyView(CapturedFingerprint capture)
    {
        var region = capture.Processing.Value.Contour;
        var index = Enumerable
            .Range(0, capture.Frame.Pixels.Length)
            .First(i => region.ContextMask[i] != 0 && region.FeatureMask[i] == 0);
        return capture.View with
        {
            Minutiae =
            [
                new FingerprintMinutia(1, (ushort)(index % 800), (ushort)(index / 800), 45, 90),
            ],
        };
    }
}
