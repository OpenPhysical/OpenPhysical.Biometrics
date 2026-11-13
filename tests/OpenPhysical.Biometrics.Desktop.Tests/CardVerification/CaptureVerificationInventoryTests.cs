// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Desktop;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class CaptureVerificationInventoryTests
{
    [Test]
    public async Task CaptureInventoryUsesCanonicalDiscoveryWithoutOpeningASession()
    {
        var driver = new Driver();
        var result = await Run(["--json", "capture", "devices"], driver);
        Assert.That(result.Code, Is.Zero);
        using var json = JsonDocument.Parse(result.Output);
        var item = json.RootElement.GetProperty("details").GetProperty("items")[0];
        Assert.That(item.GetProperty("deviceId").GetString(), Is.EqualTo(driver.Device.DeviceId));
        Assert.That(item.TryGetProperty("serial", out _), Is.False);
        Assert.That(driver.Opens, Is.Zero);
    }

    [Test]
    public async Task CaptureDiscoveryCancellationReturnsWithoutOpeningASession()
    {
        using var cancelled = new CancellationTokenSource();
        var driver = new Driver { WaitForCancellation = true };
        var execution = Run(["--json", "capture", "devices"], driver, cancelled.Token);
        await driver.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancelled.Cancel();
        var result = await execution.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(result.Code, Is.EqualTo(130));
        Assert.That(driver.Opens, Is.Zero);
    }

    private static async Task<(int Code, string Output)> Run(
        string[] arguments,
        Driver driver,
        CancellationToken token = default
    )
    {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { Args = [], DisableDefaults = true }
        );
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(
            CaptureDriverRegistry
                .Create(
                    ImmutableArray.Create<IFingerprintCaptureDriver>(driver),
                    TimeProvider.System
                )
                .Value
        );
        using var output = new StringWriter();
        var code = await StudioVerificationApplication.RunHostedAsync(
            arguments,
            builder,
            output,
            token
        );
        return (code, output.ToString());
    }

    private sealed class Driver : IFingerprintCaptureDriver
    {
        public string DriverId => "fixture";
        public CaptureDeviceDescriptor Device { get; } =
            CaptureDeviceDescriptor
                .Create(
                    "fixture-device",
                    "fixture",
                    "Fixture Scanner",
                    Maybe<string>.None,
                    CaptureCapabilities.Create(1, 255, false, true).Value
                )
                .Value;
        public int Opens { get; private set; }
        public bool WaitForCancellation { get; init; }
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<
            Result<ImmutableArray<CaptureDeviceDescriptor>, CaptureError>
        > DiscoverAsync(CancellationToken token)
        {
            Started.TrySetResult();
            if (WaitForCancellation)
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return ImmutableArray.Create(Device);
        }

        public Task<Result<IFingerprintCaptureSession, CaptureError>> OpenAsync(
            string deviceId,
            CaptureSettings settings,
            CancellationToken token
        )
        {
            Opens++;
            throw new InvalidOperationException("Inventory must not open capture.");
        }
    }
}
