// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Desktop;
using OpenPhysical.Workstation.Application;

namespace OpenPhysical.Workstation.Infrastructure.Verification;

/// <summary>Workstation capture device.</summary>
/// <param name="DeviceId">Device id.</param>
/// <param name="DisplayName">Display name.</param>
public sealed record WorkstationCaptureDevice(string DeviceId, string DisplayName);

/// <summary>Discovers bounded capture identities without acquiring a scanner session.</summary>
/// <param name="registry">Registry.</param>
/// <param name="clock">Trusted host clock.</param>
public sealed class WorkstationCaptureDevices(CaptureDriverRegistry registry, TimeProvider clock)
{
    /// <summary>List async.</summary>
    /// <param name="token">Cancellation for this operation.</param>
    /// <returns>The bounded operation result or a typed failure.</returns>
    public async Task<Result<ImmutableArray<WorkstationCaptureDevice>, WorkstationError>> ListAsync(
        CancellationToken token
    )
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
        var result = await DesktopEdge
            .TryAsync(
                () => DiscoverAsync(linked.Token),
                "Verification.Devices",
                "Scanners are unavailable."
            )
            .ConfigureAwait(false);
        return result.IsSuccess ? result.Value : Failed();
    }

    private async Task<
        Result<ImmutableArray<WorkstationCaptureDevice>, WorkstationError>
    > DiscoverAsync(CancellationToken token)
    {
        var result = await registry.DiscoverAsync(token).ConfigureAwait(false);
        if (result.IsFailure || result.Value.Length > 64)
            return Failed();
        return result
            .Value.Select(device => new WorkstationCaptureDevice(
                device.DeviceId,
                new string(device.Model.Where(value => !char.IsControl(value)).Take(256).ToArray())
            ))
            .ToImmutableArray();
    }

    private static Result<ImmutableArray<WorkstationCaptureDevice>, WorkstationError> Failed() =>
        Result.Failure<ImmutableArray<WorkstationCaptureDevice>, WorkstationError>(
            new(WorkstationErrorCodes.CredentialUnavailable)
        );
}
