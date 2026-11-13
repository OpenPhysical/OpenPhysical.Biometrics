// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text.Json.Nodes;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

[TestFixture]
internal sealed class PhotoAutoCaptureSettingsTests
{
    [TestCase(true, false)]
    [TestCase(false, true)]
    public void PhotoAndFingerprintAutomaticChoicesRoundTripIndependently(
        bool fingerprint,
        bool photo
    )
    {
        var chosen = DesktopPreferences.Create(71, fingerprint, true, 2.3).Value with
        {
            FaceAutoCapture = photo,
            FaceBackgroundReplacement = !photo,
        };
        DesktopPreferences.FromJson(chosen.ToJsonBytes()).Value.Should().Be(chosen);
        DesktopPreferences.Default.FaceAutoCapture.Should().BeFalse();
        DesktopPreferences.Default.FaceBackgroundReplacement.Should().BeTrue();
        DesktopPreferences.Default.AutoCapture.Should().BeFalse();
    }

    [TestCase("faceAutoCapture")]
    [TestCase("faceBackgroundReplacement")]
    public void PhotoChoicesMustBeExplicitAndBooleanInTheCurrentSchema(string field)
    {
        var fields = JsonNode.Parse(DesktopPreferences.Default.ToJsonBytes())!.AsObject();
        fields.Remove(field);
        DesktopPreferences
            .FromJson(System.Text.Encoding.UTF8.GetBytes(fields.ToJsonString()))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Settings.Format");
        fields[field] = "true";
        DesktopPreferences
            .FromJson(System.Text.Encoding.UTF8.GetBytes(fields.ToJsonString()))
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Settings.Format");
    }

    [Test]
    public async Task ImmediateEditsAndDefaultsPreserveIndependentChoicesAcrossFileStorage()
    {
        using var location = new SettingsTestFile();
        var store = new SettingsStorage(location.Path);
        var saved = DesktopPreferences.Create(71, true, false, 2.3).Value with
        {
            FaceAutoCapture = false,
            FaceBackgroundReplacement = false,
        };
        await store.SaveAsync(saved, CancellationToken.None);
        var applied = DesktopPreferences.Default;
        var settings = new SettingsViewModel(
            store,
            value =>
            {
                applied = value;
                return Task.FromResult(UnitResult.Success<DesktopError>());
            },
            NullLogger<SettingsViewModel>.Instance
        );
        (await settings.InitializeAsync()).Should().Succeed();
        settings.FaceAutoCapture.Should().BeFalse();
        settings.FaceBackgroundReplacement.Should().BeFalse();
        settings.AutoCapture = false;
        applied.FaceAutoCapture.Should().BeFalse();
        settings.FaceAutoCapture = true;
        applied.AutoCapture.Should().BeFalse();
        settings.Threshold = "80";
        applied.FaceBackgroundReplacement.Should().BeFalse();
        settings.FaceBackgroundReplacement = true;
        applied.FaceAutoCapture.Should().BeTrue();
        await settings.SaveCommand.ExecuteAsync(default);
        (await new SettingsStorage(location.Path).LoadAsync(CancellationToken.None))
            .Value.Should()
            .Be(applied);
        await settings.DefaultsCommand.ExecuteAsync(default);
        applied.Should().Be(DesktopPreferences.Default);
        await settings.StopAsync();
    }
}
