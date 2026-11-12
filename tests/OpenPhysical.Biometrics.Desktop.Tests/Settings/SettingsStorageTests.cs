// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class SettingsStorageTests
{
    [Test]
    public async Task FirstRunReturnsDefaultsAndSavedPreferencesSurviveANewStorageInstance()
    {
        using var location = new SettingsTestFile();
        var path = location.Path;
        var storage = new SettingsStorage(path);
        (await storage.LoadAsync(CancellationToken.None))
            .Value.Should()
            .Be(DesktopPreferences.Default);
        File.Exists(path).Should().BeFalse();
        var chosen = DesktopPreferences.Create(77, false, true, 1).Value;
        (await storage.SaveAsync(chosen, CancellationToken.None)).Should().Succeed();
        (await new SettingsStorage(path).LoadAsync(CancellationToken.None))
            .Value.Should()
            .Be(chosen);
        Directory.GetFiles(location.Folder).Should().ContainSingle();
        if (!OperatingSystem.IsWindows())
            File.GetUnixFileMode(path).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [TestCase("{")]
    [TestCase("{\"comparisonThreshold\":40,\"autoCapture\":true}")]
    [TestCase("{\"comparisonThreshold\":10001,\"autoCapture\":true,\"invertImage\":false}")]
    [TestCase("{\"comparisonThreshold\":40,\"autoCapture\":null,\"invertImage\":false}")]
    public async Task InvalidSavedDataIsReportedAndRetained(string contents)
    {
        using var location = new SettingsTestFile();
        var path = location.Path;
        Directory.CreateDirectory(location.Folder);
        await File.WriteAllTextAsync(path, contents);
        var loaded = await new SettingsStorage(path).LoadAsync(CancellationToken.None);
        loaded.Should().Fail();
        (await File.ReadAllTextAsync(path)).Should().Be(contents);
    }

    [Test]
    public async Task CancelledSaveDoesNotReplaceThePreviousPreferences()
    {
        using var location = new SettingsTestFile();
        var path = location.Path;
        var storage = new SettingsStorage(path);
        await storage.SaveAsync(DesktopPreferences.Default, CancellationToken.None);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        (
            await storage.SaveAsync(
                DesktopPreferences.Create(81, false, true, 1).Value,
                cancelled.Token
            )
        )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Cancelled");
        (await storage.LoadAsync(CancellationToken.None))
            .Value.Should()
            .Be(DesktopPreferences.Default);
        Directory.GetFiles(location.Folder).Should().ContainSingle();
    }
}
