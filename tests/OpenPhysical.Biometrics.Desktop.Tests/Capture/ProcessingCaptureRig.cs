// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class ProcessingCaptureRig : IAsyncDisposable
{
    internal ProcessingCaptureRig()
    {
        var dispatcher = Dispatcher;
        var coordinator = new CaptureCoordinator(
            Driver.Registry,
            dispatcher,
            Clock,
            NullLogger<CaptureCoordinator>.Instance
        );
        View = new CaptureViewModel(coordinator, Services.Processor, dispatcher, Clock)
        {
            AutoCapture = false,
            AutomaticExposure = false,
        };
    }

    internal DesktopPreviewDispatcher Dispatcher { get; } = new();
    internal ControlledCaptureDriver Driver { get; } = new();
    internal ControlledCaptureClock Clock { get; } = new();
    internal ScoringTestServices Services { get; } = new();
    internal CaptureViewModel View { get; }

    internal async Task<ControlledCaptureRequest> StartAsync()
    {
        await View.StartAsync(CancellationToken.None);
        return await EmptySensorTestFrames.PrimeAsync(Driver, Clock);
    }

    internal async Task<CapturedFingerprint> CaptureAsync()
    {
        (await StartAsync()).Complete(ProcessingFrameFixtures.PublicFinger()).Should().BeTrue();
        var pending = await Driver.NextAsync();
        var capturing = View.CaptureCommand.ExecuteAsync(default);
        pending.Complete().Should().BeTrue();
        await capturing;
        return View.Captured.Value;
    }

    public async ValueTask DisposeAsync()
    {
        await View.StopAsync();
        Services.Dispose();
    }
}
