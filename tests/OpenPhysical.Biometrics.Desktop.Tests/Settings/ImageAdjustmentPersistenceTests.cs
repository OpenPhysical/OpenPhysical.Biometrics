// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class ImageAdjustmentPersistenceTests
{
    [Test]
    public async Task OriginalAndResetApplyImmediatelyAndSaveSurvivesANewSettingsInstance()
    {
        using var location = new SettingsTestFile();
        var storage = new SettingsStorage(location.Path);
        var applied = DesktopPreferences.Default;
        var view = new SettingsViewModel(
            storage,
            value =>
            {
                applied = value;
                return Task.FromResult(UnitResult.Success<DesktopError>());
            },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsViewModel>.Instance
        );
        (await view.InitializeAsync()).Should().Succeed();
        view.ImageAdjustments.Brightness = 34;
        view.ImageAdjustments.Contrast = 61;
        view.ImageAdjustments.OriginalCommand.Execute(default);
        applied.ImageAdjustments.Should().Be(ImageAdjustmentSettings.Original);
        await view.SaveCommand.ExecuteAsync(default);
        await AssertRestartAsync(storage, ImageAdjustmentSettings.Original);
        view.ImageAdjustments.Brightness = -17;
        view.ImageAdjustments.ResetCommand.Execute(default);
        applied.ImageAdjustments.Should().Be(ImageAdjustmentSettings.Default);
        await view.SaveCommand.ExecuteAsync(default);
        view.Status.Should().Be("Settings saved.");
        await AssertRestartAsync(storage, ImageAdjustmentSettings.Default);
        (await view.StopAsync()).Should().Succeed();
    }

    private static async Task<bool> AssertRestartAsync(
        SettingsStorage storage,
        ImageAdjustmentSettings expected
    )
    {
        var applied = DesktopPreferences.Default;
        var restarted = new SettingsViewModel(
            storage,
            value =>
            {
                applied = value;
                return Task.FromResult(UnitResult.Success<DesktopError>());
            },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsViewModel>.Instance
        );
        (await restarted.InitializeAsync()).Should().Succeed();
        restarted.ImageAdjustments.Value.Should().Be(expected);
        applied.ImageAdjustments.Should().Be(expected);
        (await restarted.StopAsync()).Should().Succeed();
        return true;
    }
}
