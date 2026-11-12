// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class WholeContactSettingsTests
{
    [Test]
    public void SpatialPolicyIsEditableWithoutLosingUnrelatedSettings()
    {
        var view = new CapturePolicyViewModel();
        view.WholeContactWindowSize = 48;
        view.WholeContactGridStep = 12;
        view.MinimumUncorroboratedArea = 4100;
        var whole = view.Value.WholeContactOptions;
        view.EnhancementPercent = 35;
        view.ContextMargin = 40;
        view.EdgeGuardPixels = 12;
        view.Value.WholeContactOptions.Should().Be(whole);
        view.Value.Contour.Options.ContextMargin.Should().Be(40);
        view.Value.Contour.Options.ModifiedBoundaryGuard.Should().Be(12);
        view.Value.WholeContact.Evidence.Options.InnerGuard.Should().Be(12);
        view.MinimumUncorroboratedArea = 0;
        view.Value.WholeContactOptions.Should().Be(whole);
    }

    [Test]
    public void WholeContactChoicesAndCurrentAcquisitionSnapshotRoundTripExactly()
    {
        var policy = CapturePolicySettings
            .Default.WithWholeContact(
                new()
                {
                    WindowSize = 48,
                    GridStep = 12,
                    MinimumUncorroboratedArea = 4100,
                }
            )
            .Value;
        var preferences = DesktopPreferences.Default with { CapturePolicy = policy };
        var restored = DesktopPreferences.FromJson(preferences.ToJsonBytes()).Value;
        restored.CapturePolicy.Should().Be(policy);
        var acquisition = new CaptureAcquisitionPolicy(policy, 1.2);
        var loaded = CaptureAcquisitionPolicy.Read(acquisition.ToJson()).Value;
        loaded.Settings.WholeContact.Digest.Should().Be(policy.WholeContact.Digest);
        loaded.Settings.WholeContactOptions.Should().Be(policy.WholeContactOptions);
        policy.WithWholeContact(new() { MinimumUncorroboratedArea = 0 }).Should().Fail();
    }
}
