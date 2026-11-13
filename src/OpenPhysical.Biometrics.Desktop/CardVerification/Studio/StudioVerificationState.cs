// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using OpenPhysical.Workstation.Application.Devices;
using OpenPhysical.Workstation.Infrastructure.Security;
using OpenPhysical.Workstation.Infrastructure.Verification;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Current authority and available devices, containing no credential secrets.</summary>
/// <param name="Paired">The protected installation is paired.</param>
/// <param name="SignedIn">An operator session is current.</param>
/// <param name="ConfigurationReady">Signed configuration is current.</param>
/// <param name="Operators">Available protected operator identities.</param>
/// <param name="Choices">Ceremonies permitted by signed policy.</param>
/// <param name="Readers">Current opaque card-reader identities.</param>
/// <param name="Scanners">Available capture-device identities.</param>
public sealed record StudioVerificationState(
    bool Paired,
    bool SignedIn,
    bool ConfigurationReady,
    ImmutableArray<OperatorIdentityOption> Operators,
    ImmutableArray<StudioVerificationChoice> Choices,
    ImmutableArray<WorkstationReader> Readers,
    ImmutableArray<WorkstationCaptureDevice> Scanners
)
{
    /// <summary>No paired authority is loaded.</summary>
    public static StudioVerificationState Empty { get; } = new(false, false, false, [], [], [], []);

    /// <summary>The existing confidence port supplies an explicit trusted clock bound.</summary>
    public bool ClockReady { get; init; }

    /// <summary>All operator prerequisites have been authenticated.</summary>
    public bool Ready => Paired && SignedIn && ConfigurationReady && ClockReady;
}
