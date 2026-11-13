// SPDX-License-Identifier: GPL-2.0-or-later
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class CaptureVerificationConfigurationTests
{
    [TestCase("relative/libusb.dylib")]
    [TestCase(" ")]
    public async Task InvalidCaptureLibraryConfigurationFailsBeforeStartingTheHost(string path)
    {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { Args = [], DisableDefaults = true }
        );
        builder.Logging.ClearProviders();
        builder.Configuration["OpenPhysical:Capture:LibUsbPath"] = path;
        var startup = new StartupProbe();
        builder.Services.AddSingleton<IHostedService>(startup);
        using var output = new StringWriter();

        var code = await StudioVerificationApplication.RunHostedAsync(
            ["--json", "capture", "devices"],
            builder,
            output
        );

        code.Should().Be(2);
        startup.Starts.Should().Be(0);
        using var result = System.Text.Json.JsonDocument.Parse(output.ToString());
        result.RootElement.GetProperty("status").GetString().Should().Be("validation");
        result.RootElement.GetProperty("exitCode").GetInt32().Should().Be(2);
    }

    private sealed class StartupProbe : IHostedService
    {
        internal int Starts { get; private set; }

        public Task StartAsync(CancellationToken token)
        {
            Starts++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken token) => Task.CompletedTask;
    }
}
