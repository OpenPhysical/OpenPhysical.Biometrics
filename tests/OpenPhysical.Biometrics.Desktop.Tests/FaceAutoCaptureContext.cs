// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Threading;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenPhysical.Biometrics.Facial;

internal sealed class FaceAutoCaptureContext : IAsyncDisposable
{
    private readonly PortraitPreparer portraits = new(NullLogger<PortraitPreparer>.Instance);

    internal FaceAutoCaptureContext(bool empty = false)
    {
        Camera = new() { EmptyImage = empty };
        Face = new(
            Camera,
            portraits,
            new FacialRecordPreparer(NullLogger<FacialRecordPreparer>.Instance),
            new PersonEnrollmentViewModel { Name = "Example person" },
            NullLogger<FaceCaptureViewModel>.Instance
        );
        Face.Image.ShowFaceDetection = true;
    }

    internal FaceTestCameraProvider Camera { get; }
    internal FaceCaptureViewModel Face { get; }

    internal async Task StartAsync(TimeSpan delay, bool allowFixtureSmile = true)
    {
        (await Face.ConfigureAutoCaptureAsync(true, delay)).Should().Succeed();
        if (allowFixtureSmile)
            (await Face.ConfigureMouthPolicyAsync(PortraitMouthPolicy.Create(.15).Value))
                .Should()
                .Succeed();
        (await Face.ActivateAsync(true)).Should().Succeed();
        await FaceCaptureTestActions.DisplayedAsync(Face);
    }

    internal async Task ObserveAsync()
    {
        var previous = Face.Image.DetectionGuides;
        var instruction = Face.Instructions;
        await Task.Delay(370);
        Camera.PublishNext().Should().Succeed();
        await UntilAsync(() =>
            Face.HasCapture
            || !Face.Image.DetectionGuides.Equals(previous)
            || Face.Instructions != instruction
        );
    }

    internal static async Task UntilAsync(Func<bool> ready)
    {
        for (var i = 0; i < 300 && !ready(); i++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
        ready().Should().BeTrue();
    }

    public async ValueTask DisposeAsync()
    {
        await Face.StopAsync();
        portraits.Dispose();
    }
}
