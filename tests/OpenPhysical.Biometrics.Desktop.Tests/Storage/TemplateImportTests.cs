// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Fingerprints;

[TestFixture]
internal sealed class TemplateImportTests
{
    [Test]
    public async Task CbeffImportUsesItsBiometricBlockWithoutChangingThePoints()
    {
        var record = UnsignedTemplateService
            .Create([StorageFixtures.Capture()], new TemplateMetadata(1, 0, 0))
            .Value;
        var fmr = Incits378Codec.Write(record).Value;
        var header = CbeffHeader
            .Create(
                (uint)fmr.Length,
                0,
                0x001B,
                0x0201,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch.AddYears(1),
                8,
                0x80,
                80,
                new byte[18],
                new byte[25]
            )
            .Value;
        var bytes = CbeffCodec.Write(new CbeffRecord(header, ImmutableArray.Create(fmr), [])).Value;
        var imported = await OpenAsync(bytes);
        imported.Should().Succeed();
        Incits378Codec.Write(imported.Value.Record).Value.Should().Equal(fmr);
    }

    [TestCase(new byte[] { 3, 0, 0 })]
    [TestCase(new byte[] { 1, 2, 3 })]
    public async Task TruncatedAndUnknownInputsReturnTypedFailures(byte[] bytes)
    {
        (await OpenAsync(bytes)).Should().Fail().Which.Code.Should().Be("Storage.Record");
    }

    [Test]
    public async Task ZeroViewRecordReturnsTypedFailure()
    {
        var record = UnsignedTemplateService
            .Create([StorageFixtures.Capture()], new(1, 0, 0))
            .Value;
        var bytes = Incits378Codec.Write(record).Value;
        bytes[24] = 0;
        (await OpenAsync(bytes)).Should().Fail().Which.Code.Should().Be("Storage.Record");
    }

    private static async Task<CSharpFunctionalExtensions.Result<
        TemplateDocument,
        DesktopError
    >> OpenAsync(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".fmr");
        await File.WriteAllBytesAsync(path, bytes);
        var result = await new TemplateStorage().LoadAsync(path, CancellationToken.None);
        File.Delete(path);
        return result;
    }
}
