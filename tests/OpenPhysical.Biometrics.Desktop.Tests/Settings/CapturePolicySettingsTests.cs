// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;

[TestFixture]
internal sealed class CapturePolicySettingsTests
{
    [Test]
    public async Task AdvancedPolicySurvivesStorageAndUnrelatedApplicationEdits()
    {
        using var location = new SettingsTestFile();
        var policy = EditedPolicy();
        var storage = new SettingsStorage(location.Path);
        var preferences = DesktopPreferences.Default with { CapturePolicy = policy };
        (await storage.SaveAsync(preferences, CancellationToken.None)).Should().Succeed();
        var applied = DesktopPreferences.Default;
        var view = new SettingsViewModel(
            storage,
            value =>
            {
                applied = value;
                return UnitResult.Success<DesktopError>();
            }
        );
        (await view.InitializeAsync()).Should().Succeed();
        view.Threshold = "65";
        view.AutoCaptureDelaySeconds = "2";
        view.AutoCapture = false;
        view.Invert = true;
        applied.CapturePolicy.Should().Be(policy);
        await view.StopAsync();
    }

    [Test]
    public void UnsafePolicyCannotBeInstalled()
    {
        var defaults = CapturePolicySettings.Default;
        CapturePolicySettings
            .Create(
                defaults.Enhancement,
                defaults.EvidenceOptions with
                {
                    MinimumSignal = -1,
                },
                defaults.EmptySensorOptions,
                1,
                5,
                defaults.ContextMargin
            )
            .Should()
            .Fail();
        CapturePolicySettings
            .Create(
                defaults.Enhancement,
                defaults.EvidenceOptions,
                defaults.EmptySensorOptions,
                0,
                5,
                defaults.ContextMargin
            )
            .Should()
            .Fail();
    }

    private static OpenPhysical.Biometrics.Capture.Futronic.Fs50WholeContactOptions WholeOptions() =>
        new()
        {
            WindowSize = 48,
            GridStep = 12,
            MinimumUncorroboratedArea = 4100,
        };

    private static CapturePolicySettings EditedPolicy()
    {
        var defaults = CapturePolicySettings.Default;
        return CapturePolicySettings
            .Create(
                EnhancementSettings.Create(48, 3, 0.35).Value,
                defaults.EvidenceOptions with
                {
                    InnerGuard = 12,
                    RidgeBoundaryTolerance = 48,
                },
                defaults.EmptySensorOptions with
                {
                    RequiredFrames = 5,
                },
                2,
                7,
                48
            )
            .Bind(settings =>
                settings.WithPresentation(
                    FingerCapturePolicy
                        .Create(
                            3,
                            TimeSpan.FromMilliseconds(600),
                            TimeSpan.FromMilliseconds(1200),
                            0.25,
                            0.45,
                            3,
                            2
                        )
                        .Value
                )
            )
            .Bind(settings => settings.WithWholeContact(WholeOptions()))
            .Value;
    }
}
