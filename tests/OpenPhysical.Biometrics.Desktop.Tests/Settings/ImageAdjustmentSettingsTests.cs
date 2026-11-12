// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class ImageAdjustmentSettingsTests
{
    [Test]
    public void OriginalIsNeutralAndResetRestoresTheApplicationPolicy()
    {
        var settings = new ImageAdjustmentViewModel();
        settings.Value.Should().Be(ImageAdjustmentSettings.Default);
        settings.AutomaticEnhancement = true;
        settings.Brightness = 17;
        settings.Contrast = 42;
        settings.OriginalCommand.Execute(default);
        settings.AutomaticEnhancement.Should().BeFalse();
        settings.Contrast.Should().Be(0);
        settings.Brightness = -15;
        settings.ResetCommand.Execute(default);
        settings.Value.Should().Be(ImageAdjustmentSettings.Default);
    }

    [TestCase(-101, 20)]
    [TestCase(101, 20)]
    [TestCase(0, -1)]
    [TestCase(0, 101)]
    public void InvalidAdjustmentsFailInsteadOfChangingProcessing(int brightness, int contrast) =>
        ImageAdjustmentSettings.Create(true, brightness, contrast).Should().Fail();

    [Test]
    public void EachValidEditPublishesOneImmutableRevision()
    {
        var settings = new ImageAdjustmentViewModel();
        var changes = new List<ImageAdjustmentSettings>();
        settings.Changed += value => changes.Add(value);
        settings.Brightness = 12;
        settings.Contrast = 33;
        changes.Should().HaveCount(2);
        changes[0].Brightness.Should().Be(12);
        changes[0].Contrast.Should().Be(ImageAdjustmentSettings.Default.Contrast);
        changes[1].Brightness.Should().Be(changes[0].Brightness);
        changes[1].Contrast.Should().Be(33);
    }

    [Test]
    public void PreferencesRoundTripCurrentControls()
    {
        var chosen = DesktopPreferences.Default with
        {
            ImageAdjustments = ImageAdjustmentSettings.Create(false, -12, 42).Value,
        };
        DesktopPreferences.FromJson(chosen.ToJsonBytes()).Value.Should().Be(chosen);
    }
}
