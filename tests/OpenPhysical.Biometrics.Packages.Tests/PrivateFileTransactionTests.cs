// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages.Tests;

using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class PrivateFileTransactionTests
{
    private string root = string.Empty;

    [SetUp]
    public void CreatePrivateTestRoot()
    {
        root = Path.Combine(
            OperatingSystem.IsMacOS() ? "/private/tmp" : Path.GetTempPath(),
            "opbio-transaction-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(root);
    }

    [TearDown]
    public void RemovePrivateTestRoot() => Directory.Delete(root, true);

    [Test]
    public async Task BatchPreflightPreservesEveryOriginalAndSuccessfulInstallUsesPrivatePermissions()
    {
        var first = Path.Combine(root, "first.bin");
        var second = Path.Combine(root, "second.bin");
        File.WriteAllText(first, "old first");
        File.WriteAllText(second, "old second");
        var files = ImmutableArray.Create(
            new PrivateFileWrite(first, [1]),
            new PrivateFileWrite(second, [2])
        );
        (await PrivateFileTransaction.WriteBatchAsync(files, false))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.DestinationExists);
        File.ReadAllText(first).Should().Be("old first");
        File.ReadAllText(second).Should().Be("old second");
        (await PrivateFileTransaction.WriteBatchAsync(files, true)).Should().Succeed();
        File.ReadAllBytes(first).Should().Equal(1);
        File.ReadAllBytes(second).Should().Equal(2);
        Directory.GetFileSystemEntries(root).Should().HaveCount(2);
        if (!OperatingSystem.IsWindows())
            File.GetUnixFileMode(first).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Test]
    public async Task RepositoriesAndLinkedAncestorsAreRejectedWithoutCreatingOutput()
    {
        var repo = Path.Combine(root, "repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        var target = Path.Combine(repo, "private.bin");
        (await PrivateFileTransaction.WriteBatchAsync([new(target, [1])], false))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.UnsafePath);
        File.Exists(target).Should().BeFalse();
        if (OperatingSystem.IsWindows())
            return;
        var linked = Path.Combine(root, "linked");
        Directory.CreateSymbolicLink(linked, repo);
        (
            await PrivateFileTransaction.WriteBatchAsync(
                [new(Path.Combine(linked, "private.bin"), [1])],
                false
            )
        )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.UnsafePath);
    }

    [Test]
    public async Task CancellationLeavesTheExistingFileAndNoStageOrBackup()
    {
        var path = Path.Combine(root, "existing.bin");
        File.WriteAllText(path, "original");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        (await PrivateFileTransaction.WriteBatchAsync([new(path, [1])], true, cancellation.Token))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.Cancelled);
        File.ReadAllText(path).Should().Be("original");
        Directory.GetFileSystemEntries(root).Should().Equal(path);
    }

    [Test]
    public async Task ConflictingFileAndDirectoryOutputsCannotLeaveAnEarlierReplacementInstalled()
    {
        var original = Path.Combine(root, "original.bin");
        var conflicting = Path.Combine(root, "conflicting.bin");
        File.WriteAllText(original, "original contents");
        var files = ImmutableArray.Create(
            new PrivateFileWrite(original, [1]),
            new PrivateFileWrite(conflicting, [2]),
            new PrivateFileWrite(Path.Combine(conflicting, "nested.bin"), [3])
        );
        (await PrivateFileTransaction.WriteBatchAsync(files, true)).Should().Fail();
        File.ReadAllText(original).Should().Be("original contents");
        File.Exists(Path.Combine(conflicting, "nested.bin")).Should().BeFalse();
        Directory.GetFiles(root, "*.stage", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.GetFiles(root, "*.backup", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Test]
    public async Task PackageStoreWritesOnlyOpaqueEncryptedFileAndReopensIt()
    {
        using var secret = PackageSecret.Create("public test passphrase"u8).Value;
        var saved = await PackageFileStore.SaveAsync(
            root,
            PackageCodecTests.Example(),
            secret,
            PackageProtectionPolicy.Default
        );
        saved.Should().Succeed();
        Path.GetExtension(saved.Value).Should().Be(".opbio");
        Path.GetFileName(saved.Value).Should().NotContain("Example");
        Directory.GetFiles(root).Should().Equal(saved.Value);
        var opened = await PackageFileStore.OpenAsync(
            saved.Value,
            secret,
            PackageProtectionPolicy.Default
        );
        opened.Should().Succeed();
        opened.Value.Label.Should().Be("Example Person");
        File.WriteAllBytes(saved.Value, [1, 2, 3]);
        (await PackageFileStore.OpenAsync(saved.Value, secret, PackageProtectionPolicy.Default))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.InvalidEnvelope);
    }
}
