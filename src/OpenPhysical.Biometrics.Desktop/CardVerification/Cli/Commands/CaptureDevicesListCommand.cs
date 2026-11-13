// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Middleware.Cli.Commands;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Desktop;
using OpenPhysical.Cli.Hosting;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>Lists canonical fingerprint device identities without opening capture sessions.</summary>
/// <param name="devices">Configured capture drivers.</param>
/// <param name="renderer">Renderer.</param>
/// <param name="output">Bounded command output destination.</param>
/// <param name="console">Selected interactive console.</param>
/// <param name="clock">Trusted host clock.</param>
/// <param name="cancellationToken">Cancellation for this operation.</param>
public sealed class CaptureDevicesListCommand(
    CaptureDriverRegistry devices,
    OperationResultRenderer renderer,
    CommandOutputOptions output,
    IAnsiConsole console,
    TimeProvider clock,
    CancellationToken cancellationToken = default
) : AsyncCommand<CaptureDevicesListSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        CaptureDevicesListSettings settings
    )
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token
        );
        var attempted = await DesktopEdge
            .TryAsync(
                () => devices.DiscoverAsync(cancelled.Token).WaitAsync(cancelled.Token),
                "Capture.DiscoveryUnavailable",
                "Capture discovery could not finish."
            )
            .ConfigureAwait(false);
        if (attempted.IsFailure)
            return renderer.RenderFailure(
                new(
                    cancellationToken.IsCancellationRequested
                        ? OperationFailureKind.Cancelled
                        : OperationFailureKind.Unavailable,
                    Guid.NewGuid()
                )
            );
        var inventory = attempted.Value;
        return inventory.IsFailure
            ? RenderDiscoveryFailure(inventory.Error, timeout.IsCancellationRequested)
            : RenderInventory(inventory.Value);
    }

    private int RenderDiscoveryFailure(CaptureError error, bool timedOut) =>
        renderer.RenderFailure(
            new(
                cancellationToken.IsCancellationRequested
                || error.Code == CaptureErrorCodes.Cancelled && !timedOut
                    ? OperationFailureKind.Cancelled
                    : OperationFailureKind.Unavailable,
                Guid.NewGuid()
            )
        );

    private int RenderInventory(ImmutableArray<CaptureDeviceDescriptor> inventory)
    {
        var details = JsonSerializer.SerializeToElement(
            new
            {
                kind = "fingerprintDevices",
                items = inventory
                    .Select(value => new
                    {
                        deviceId = value.DeviceId,
                        driverId = value.DriverId,
                        model = value.Model,
                    })
                    .ToArray(),
            }
        );
        if (System.Text.Encoding.UTF8.GetByteCount(details.GetRawText()) > 262144)
            return renderer.RenderFailure(new(OperationFailureKind.Unavailable, Guid.NewGuid()));
        if (!output.Json)
        {
            if (inventory.IsEmpty)
                console.WriteLine(
                    "No fingerprint devices found. Configure a capture driver before discovery."
                );
            foreach (var device in inventory)
                console.WriteLine($"{device.DeviceId}: {device.Model} ({device.DriverId})");
        }
        return renderer.RenderInspection(details);
    }
}
