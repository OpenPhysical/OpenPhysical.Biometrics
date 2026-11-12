// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class CaptureDelaySettingsTests
{
    [TestCase(0)]
    [TestCase(0.1)]
    [TestCase(1)]
    [TestCase(10)]
    public void FiniteDelayRoundTripsInCurrentSchema(double seconds)
    {
        var chosen = DesktopPreferences.Create(71, false, true, seconds).Value;
        var bytes = chosen.ToJsonBytes();
        Encoding.UTF8.GetString(bytes).Should().Contain("\"schemaVersion\":1");
        DesktopPreferences.FromJson(bytes).Value.Should().Be(chosen);
    }

    [TestCase(-0.1)]
    [TestCase(10.1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void InvalidDelayIsASettingsFailure(double seconds) =>
        DesktopPreferences
            .Create(40, true, false, seconds)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Settings.Delay");

    [Test]
    public void ObsoleteUnversionedPreferencesAreRejected()
    {
        var old = Encoding.UTF8.GetBytes(
            "{\"comparisonThreshold\":73,\"autoCapture\":false,\"invertImage\":true}"
        );
        DesktopPreferences.FromJson(old).Should().Fail().Which.Code.Should().Be("Settings.Format");
    }

    [Test]
    public async Task DelayEditsApplyAndPersistAndDefaultsRestoreOneSecond()
    {
        using var location = new SettingsTestFile();
        var applied = DesktopPreferences.Default;
        var store = new SettingsStorage(location.Path);
        var view = new SettingsViewModel(
            store,
            preferences =>
            {
                applied = preferences;
                return UnitResult.Success<DesktopError>();
            }
        );
        view.AutoCaptureDelaySeconds = "0.3";
        applied.AutoCaptureDelaySeconds.Should().Be(0.3);
        await view.SaveCommand.ExecuteAsync(default);
        (await store.LoadAsync(CancellationToken.None))
            .Value.AutoCaptureDelaySeconds.Should()
            .Be(0.3);
        view.AutoCaptureDelaySeconds = "NaN";
        await view.SaveCommand.ExecuteAsync(default);
        view.Status.Should().Contain("0").And.Contain("10");
        applied.AutoCaptureDelaySeconds.Should().Be(0.3);
        (await store.LoadAsync(CancellationToken.None))
            .Value.AutoCaptureDelaySeconds.Should()
            .Be(0.3);
        await view.DefaultsCommand.ExecuteAsync(default);
        view.AutoCaptureDelaySeconds.Should().Be("1");
        applied.AutoCaptureDelaySeconds.Should().Be(1);
        await view.StopAsync();
    }
}
