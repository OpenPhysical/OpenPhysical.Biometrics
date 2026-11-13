// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;

[TestFixture]
internal sealed class SettingsViewModelTests
{
    [Test]
    public async Task ValidEditsApplyImmediatelyAndSaveAndDefaultsPersistTheirValues()
    {
        using var location = new SettingsTestFile();
        var path = location.Path;
        var applied = DesktopPreferences.Default;
        var storage = new SettingsStorage(path);
        var view = new SettingsViewModel(
            storage,
            preferences =>
            {
                applied = preferences;
                return Task.FromResult(UnitResult.Success<DesktopError>());
            },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsViewModel>.Instance
        );
        await view.InitializeAsync();
        view.Threshold = "83";
        view.AutoCapture = false;
        view.Invert = true;
        applied.Should().Be(DesktopPreferences.Create(83, false, true, 1).Value);
        await view.SaveCommand.ExecuteAsync(default);
        (await new SettingsStorage(path).LoadAsync(CancellationToken.None))
            .Value.Should()
            .Be(applied);
        await view.DefaultsCommand.ExecuteAsync(default);
        applied.Should().Be(DesktopPreferences.Default);
        (await storage.LoadAsync(CancellationToken.None))
            .Value.Should()
            .Be(DesktopPreferences.Default);
        view.Threshold.Should().Be("40");
        view.AutoCapture.Should().BeFalse();
        view.Invert.Should().BeFalse();
        await view.StopAsync();
        view.SaveCommand.CanExecute(default).Should().BeFalse();
    }

    [Test]
    public async Task InvalidThresholdDoesNotApplyOrOverwriteSavedSettings()
    {
        using var location = new SettingsTestFile();
        var path = location.Path;
        var storage = new SettingsStorage(path);
        var applied = DesktopPreferences.Default;
        var view = new SettingsViewModel(
            storage,
            preferences =>
            {
                applied = preferences;
                return Task.FromResult(UnitResult.Success<DesktopError>());
            },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsViewModel>.Instance
        );
        await view.SaveCommand.ExecuteAsync(default);
        view.Threshold = "not a number";
        view.AutoCapture = false;
        await view.SaveCommand.ExecuteAsync(default);
        view.Status.Should().Contain("0").And.Contain("10000");
        applied.Should().Be(DesktopPreferences.Create(40, false, false, 1).Value);
        (await storage.LoadAsync(CancellationToken.None))
            .Value.Should()
            .Be(DesktopPreferences.Default);
        await view.StopAsync();
    }
}
