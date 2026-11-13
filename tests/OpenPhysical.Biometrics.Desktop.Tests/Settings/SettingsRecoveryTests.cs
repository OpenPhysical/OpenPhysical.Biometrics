// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class SettingsRecoveryTests
{
    [TestCase("{")]
    [TestCase("{\"comparisonThreshold\":40}")]
    public async Task InvalidSettingsApplyAndPersistDefaultsInsteadOfLeavingASchemaError(
        string invalid
    )
    {
        using var location = new SettingsTestFile();
        Directory.CreateDirectory(location.Folder);
        await File.WriteAllTextAsync(location.Path, invalid);
        var applied = DesktopPreferences.Create(75, false, true, 2).Value;
        var view = new SettingsViewModel(
            new SettingsStorage(location.Path),
            preferences =>
            {
                applied = preferences;
                return Task.FromResult(UnitResult.Success<DesktopError>());
            },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsViewModel>.Instance
        );
        (await view.InitializeAsync()).Should().Succeed();
        applied.Should().Be(DesktopPreferences.Default);
        (await new SettingsStorage(location.Path).LoadAsync(CancellationToken.None))
            .Should()
            .Succeed()
            .Which.Should()
            .Be(DesktopPreferences.Default);
        view.Status.Should().NotContain("schema").And.NotContain("Settings.");
        view.Notice.Value.Message.Should().Be("Unable to load settings, resetting.");
        view.Notice.Value.IsError.Should().BeFalse();
        await view.InitializeAsync();
        view.Notice.HasNoValue.Should().BeTrue();
    }

    [Test]
    [Platform(Exclude = "Win")]
    public async Task UnreadableSettingsAreReplacedWithoutRequiringTheOperatorToEditFiles()
    {
        if (OperatingSystem.IsWindows())
            return;
        using var location = new SettingsTestFile();
        Directory.CreateDirectory(location.Folder);
        await File.WriteAllBytesAsync(
            location.Path,
            DesktopPreferences.Create(75, false, true, 2).Value.ToJsonBytes()
        );
        File.SetUnixFileMode(location.Path, 0);
        try
        {
            var view = new SettingsViewModel(
                new SettingsStorage(location.Path),
                _ => Task.FromResult(UnitResult.Success<DesktopError>()),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsViewModel>.Instance
            );
            (await view.InitializeAsync()).Should().Succeed();
            (await new SettingsStorage(location.Path).LoadAsync(CancellationToken.None))
                .Should()
                .Succeed()
                .Which.Should()
                .Be(DesktopPreferences.Default);
        }
        finally
        {
            File.SetUnixFileMode(location.Path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Test]
    public async Task FailedRecoveryStillAppliesDefaultsAndDoesNotClaimTheyWereSaved()
    {
        using var location = new SettingsTestFile();
        Directory.CreateDirectory(location.Path);
        var applied = DesktopPreferences.Create(75, false, true, 2).Value;
        var view = new SettingsViewModel(
            new SettingsStorage(location.Path),
            preferences =>
            {
                applied = preferences;
                return Task.FromResult(UnitResult.Success<DesktopError>());
            },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsViewModel>.Instance
        );
        (await view.InitializeAsync()).Should().Fail();
        applied.Should().Be(DesktopPreferences.Default);
        view.Status.Should().Contain("could not be saved").And.NotContain("Settings.");
        Directory.Exists(location.Path).Should().BeTrue();
        view.Notice.Value.IsError.Should().BeTrue();
    }

    [Test]
    public async Task FirstRunUsesDefaultsWithoutARecoveryNoticeOrAnUnrequestedWrite()
    {
        using var location = new SettingsTestFile();
        var view = new SettingsViewModel(
            new SettingsStorage(location.Path),
            _ => Task.FromResult(UnitResult.Success<DesktopError>()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsViewModel>.Instance
        );
        (await view.InitializeAsync()).Should().Succeed();
        view.Notice.HasNoValue.Should().BeTrue();
        File.Exists(location.Path).Should().BeFalse();
    }

    [Test]
    public async Task FailedSaveExplainsThatCurrentChoicesWereNotPersisted()
    {
        using var location = new SettingsTestFile();
        var view = new SettingsViewModel(
            new SettingsStorage(location.Path),
            _ => Task.FromResult(UnitResult.Success<DesktopError>()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsViewModel>.Instance
        );
        await view.InitializeAsync();
        Directory.CreateDirectory(location.Path);
        view.Threshold = "95";
        await view.SaveCommand.ExecuteAsync(default);
        view.Status.Should()
            .Contain("Unable to save")
            .And.Contain("this session")
            .And.NotContain("Settings.Write");
        view.Notice.Value.IsError.Should().BeTrue();
        Directory.Exists(location.Path).Should().BeTrue();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task FailedApplicationKeepsItsTypedErrorAndShowsAFriendlyOperatorNotice(
        bool invalidFile
    )
    {
        using var location = new SettingsTestFile();
        if (invalidFile)
        {
            Directory.CreateDirectory(location.Folder);
            await File.WriteAllTextAsync(location.Path, "{");
        }
        var failure = new DesktopError(
            "Fixture.SpecificFailure",
            "Detailed application diagnostic."
        );
        var view = new SettingsViewModel(
            new SettingsStorage(location.Path),
            _ => Task.FromResult(UnitResult.Failure(failure)),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsViewModel>.Instance
        );
        (await view.InitializeAsync()).Should().Fail().Which.Should().Be(failure);
        view.Notice.HasValue.Should().BeTrue();
        view.Notice.Value.Message.Should()
            .Contain("could not be applied")
            .And.NotContain(failure.Message);
        view.Notice.Value.IsError.Should().BeTrue();
        view.Status.Should().NotContain(failure.Code).And.NotContain(failure.Message);
        File.Exists(location.Path).Should().Be(invalidFile);
    }
}
