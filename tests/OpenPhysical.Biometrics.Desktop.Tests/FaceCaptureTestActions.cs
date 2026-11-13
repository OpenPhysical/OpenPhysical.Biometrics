// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Threading;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;

internal static class FaceCaptureTestActions
{
    internal static async Task ManualPreviewAsync(FaceCaptureViewModel face)
    {
        (await face.ConfigureAutoCaptureAsync(false, TimeSpan.FromSeconds(1))).Should().Succeed();
        await DisplayedAsync(face);
    }

    internal static async Task OverrideRejectedAsync(FaceCaptureViewModel face)
    {
        if (!face.RequiresRetake)
            return;
        face.Actions.Override.CanExecute(default).Should().BeTrue();
        await face.Actions.Override.ExecuteAsync(default);
    }

    internal static async Task DisplayedAsync(FaceCaptureViewModel face)
    {
        for (var i = 0; i < 100 && face.Image.Scene.HasNoValue; i++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
        face.Image.Scene.HasValue.Should().BeTrue();
    }
}
