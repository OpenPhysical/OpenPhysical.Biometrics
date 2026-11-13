// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text.Json;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Facial;

internal sealed class MouthPolicySettingsTests
{
    [Test]
    public void MouthPolicyRoundTripsAndCannotBeSilentlyOmittedOrReplacedWithAnInvalidRatio()
    {
        var chosen = DesktopPreferences.Default with
        {
            MouthPolicy = PortraitMouthPolicy.Create(.07).Value,
        };
        DesktopPreferences.FromJson(chosen.ToJsonBytes()).Value.Should().Be(chosen);
        using var document = JsonDocument.Parse(chosen.ToJsonBytes());
        var fields = document
            .RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.Clone());
        fields.Remove("mouthMaximumOpeningRatio");
        DesktopPreferences
            .FromJson(JsonSerializer.SerializeToUtf8Bytes(fields))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Settings.Format");
        fields["mouthMaximumOpeningRatio"] = JsonSerializer.SerializeToElement(1);
        DesktopPreferences
            .FromJson(JsonSerializer.SerializeToUtf8Bytes(fields))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Settings.MouthTolerance");
    }

    [Test]
    public async Task PercentEditsApplyAndSaveWaitsForTheCurrentPolicyThenDefaultsRestoreFourPercent()
    {
        using var location = new SettingsTestFile();
        var storage = new SettingsStorage(location.Path);
        var applied = DesktopPreferences.Default;
        var pending = new TaskCompletionSource<UnitResult<DesktopError>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var view = new SettingsViewModel(
            storage,
            async value =>
            {
                applied = value;
                return value.MouthPolicy.MaximumOpeningRatio == .07
                    ? await pending.Task
                    : UnitResult.Success<DesktopError>();
            },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsViewModel>.Instance
        );
        (await view.InitializeAsync()).Should().Succeed();
        view.MouthTolerancePercent = "7";
        applied.MouthPolicy.MaximumOpeningRatio.Should().Be(.07);
        var save = view.SaveCommand.ExecuteAsync(default);
        save.IsCompleted.Should().BeFalse();
        File.Exists(location.Path).Should().BeFalse();
        pending.SetResult(UnitResult.Success<DesktopError>());
        await save;
        (await storage.LoadAsync(CancellationToken.None))
            .Value.MouthPolicy.MaximumOpeningRatio.Should()
            .Be(.07);
        view.MouthTolerancePercent = "NaN";
        await view.SaveCommand.ExecuteAsync(default);
        applied.MouthPolicy.MaximumOpeningRatio.Should().Be(.07);
        view.Status.Should().Contain("100");
        await view.DefaultsCommand.ExecuteAsync(default);
        view.MouthTolerancePercent.Should().Be("4");
        applied.MouthPolicy.Should().Be(PortraitMouthPolicy.Default);
        (await view.StopAsync()).Should().Succeed();
    }

    [Test]
    public async Task AnApplicationFailureIsObservedAndDoesNotPersistTheRejectedConfiguration()
    {
        using var location = new SettingsTestFile();
        var storage = new SettingsStorage(location.Path);
        var view = new SettingsViewModel(
            storage,
            value =>
                Task.FromResult(
                    value.MouthPolicy.MaximumOpeningRatio == .07
                        ? UnitResult.Failure(
                            new DesktopError("Face.Policy", "The policy could not be applied.")
                        )
                        : UnitResult.Success<DesktopError>()
                ),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsViewModel>.Instance
        );
        await view.InitializeAsync();
        view.MouthTolerancePercent = "7";
        await view.SaveCommand.ExecuteAsync(default);
        view.Status.Should().Contain("could not be applied");
        File.Exists(location.Path).Should().BeFalse();
        await view.StopAsync();
    }
}
