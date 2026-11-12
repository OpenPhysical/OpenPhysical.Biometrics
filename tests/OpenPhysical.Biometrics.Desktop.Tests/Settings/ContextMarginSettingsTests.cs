// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text.Json.Nodes;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class ContextMarginSettingsTests
{
    [Test]
    public void ContextPolicyReusesTheExistingConfiguredGuard()
    {
        var settings = CapturePolicySettings.Default;
        settings
            .Contour.Options.ModifiedBoundaryGuard.Should()
            .Be(settings.EvidenceOptions.InnerGuard);
        Chosen(48, 12).Contour.Options.ModifiedBoundaryGuard.Should().Be(12);
    }

    [TestCase(-1)]
    [TestCase(129)]
    public void OutOfRangeMarginsCannotBecomeConfiguration(int margin)
    {
        var defaults = CapturePolicySettings.Default;
        CapturePolicySettings
            .Create(
                defaults.Enhancement,
                defaults.EvidenceOptions,
                defaults.EmptySensorOptions,
                defaults.MinimumNfiq1Gain,
                defaults.MinimumNfiq2Gain,
                margin
            )
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Settings.ContextMargin");
    }

    [Test]
    public async Task MarginAndOtherChoicesSurviveRealFileStorageAndUnrelatedUiEdits()
    {
        using var location = new SettingsTestFile();
        var storage = new SettingsStorage(location.Path);
        var saved = DesktopPreferences.Create(71, false, true, 2.3).Value with
        {
            CapturePolicy = Chosen(48, 12),
        };
        (await storage.SaveAsync(saved, CancellationToken.None)).Should().Succeed();
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
        view.ContextMargin.Should().Be(48);
        view.ContextMargin = 40;
        view.CapturePolicy.EdgeGuardPixels = 10;
        view.CapturePolicy.EnhancementPercent = 35;
        applied.CapturePolicy.ContextMargin.Should().Be(40);
        applied.CapturePolicy.Contour.Options.ModifiedBoundaryGuard.Should().Be(10);
        applied.CapturePolicy.Enhancement.Blend.Should().Be(0.35);
        applied.ComparisonThreshold.Should().Be(71);
        applied.AutoCaptureDelaySeconds.Should().Be(2.3);
        applied.InvertImage.Should().BeTrue();
        await view.SaveCommand.ExecuteAsync(default);
        var restored = (
            await new SettingsStorage(location.Path).LoadAsync(CancellationToken.None)
        ).Value;
        restored.Should().Be(applied);
        await view.DefaultsCommand.ExecuteAsync(default);
        view.ContextMargin.Should().Be(32);
        await view.StopAsync();
    }

    [Test]
    public void AcquisitionSnapshotBindsTheMarginAndRequiresAllCurrentFields()
    {
        var policy = new CaptureAcquisitionPolicy(Chosen(48, 12), 1.7);
        var json = policy.ToJson();
        json.GetProperty("capturePolicy").GetProperty("contextMargin").GetInt32().Should().Be(48);
        CaptureAcquisitionPolicy.Read(json).Value.Settings.Should().Be(policy.Settings);
        json.GetRawText()
            .Should()
            .NotBe(new CaptureAcquisitionPolicy(Chosen(40, 12), 1.7).ToJson().GetRawText());
        var incomplete = JsonNode.Parse(json.GetRawText())!.AsObject();
        incomplete["capturePolicy"]!.AsObject().Remove("contextMargin");
        using var document = System.Text.Json.JsonDocument.Parse(incomplete.ToJsonString());
        CaptureAcquisitionPolicy.Read(document.RootElement).Should().Fail();
    }

    private static CapturePolicySettings Chosen(int margin, int guard)
    {
        var defaults = CapturePolicySettings.Default;
        return CapturePolicySettings
            .Create(
                defaults.Enhancement,
                defaults.EvidenceOptions with
                {
                    InnerGuard = guard,
                },
                defaults.EmptySensorOptions,
                defaults.MinimumNfiq1Gain,
                defaults.MinimumNfiq2Gain,
                margin
            )
            .Value;
    }
}
