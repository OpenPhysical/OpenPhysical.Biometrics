// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Usb;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
[NonParallelizable]
internal sealed class MissingScannerRuntimeTests
{
    [AvaloniaTest]
    public async Task MissingScannerRuntimeLeavesTheWorkspaceAvailableAndReportsATypedFailure()
    {
        using var assets = new WithheldScannerRuntime();
        var host = DesktopServices.CreateHost();
        await using var ownedHost = (IAsyncDisposable)host;
        var workspace = host.Services.GetRequiredService<WorkspaceViewModel>();
        var provider = host.Services.GetRequiredService<IUsbDeviceProvider>();
        var discovered = await provider.DiscoverAsync(1, 1, CancellationToken.None);
        discovered.Should().Fail().Which.Code.Should().Be(CaptureErrorCodes.DeviceUnavailable);
        discovered.Error.Message.Should().Contain("libusb");
        workspace.Capture.CaptureCommand.CanExecute(default).Should().BeFalse();
        (await workspace.Capture.StopAsync()).Should().Succeed();
        (await workspace.Inspection.StopAsync()).Should().Succeed();
        (await workspace.Export.StopAsync()).Should().Succeed();
        (await workspace.Settings.StopAsync()).Should().Succeed();
    }
}

internal sealed class WithheldScannerRuntime : IDisposable
{
    private readonly List<(string Original, string Backup)> moved = [];

    internal WithheldScannerRuntime()
    {
        try
        {
            for (var index = 0; index < 4; index++)
            {
                var located = LibUsbDeviceProvider.LocateBundledLibrary();
                if (located.IsFailure)
                    return;
                var backup = located.Value + ".withheld-" + Guid.NewGuid().ToString("N");
                File.Move(located.Value, backup);
                moved.Add((located.Value, backup));
            }
            Assert.Fail("Runtime discovery did not exhaust the withheld application assets.");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var (original, backup) in moved.AsEnumerable().Reverse())
            File.Move(backup, original);
        moved.Clear();
    }
}
