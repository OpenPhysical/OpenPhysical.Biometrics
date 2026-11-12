// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class FrozenFrameIdentityTests
{
    [AvaloniaTest]
    public async Task EarlierBestFrameAndItsTonePolicySurviveAnEditBeforeFirstExtractionCompletes()
    {
        await using var context = new FrozenSelectionTestContext();
        var frozen = await context.SelectEarlierAsync();
        context.View.Scene.Value.Foreground.Should().BeEmpty();
        context.View.LiveFrame.Value.Frame.Should().BeSameAs(frozen);
        context.View.LiveFrame.Value.Processing.Value.OriginalFrame.Should().BeSameAs(frozen);
        context.View.LiveFrame.Value.Processing.Value.Policy.Brightness.Should().Be(7);
        context
            .View.ConfigureImageAdjustments(
                ImageAdjustmentSettings.Create(false, 9, 0).Value,
                CapturePolicySettings.Default
            )
            .Should()
            .Succeed();
        await context.AwaitRevisionAsync();
        context.View.LiveFrame.Value.Frame.Should().BeSameAs(frozen);
        context.View.LiveFrame.Value.Processing.Value.OriginalFrame.Should().BeSameAs(frozen);
        context.View.LiveFrame.Value.Processing.Value.Policy.Brightness.Should().Be(9);
    }
}
