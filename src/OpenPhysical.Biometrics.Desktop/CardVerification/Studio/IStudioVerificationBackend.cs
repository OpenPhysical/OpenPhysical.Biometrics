// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Workstation.Application.Pairing;
using OpenPhysical.Workstation.Application.Verification;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Application operations backed by the existing paired verification services.</summary>
public interface IStudioVerificationBackend : IAsyncDisposable
{
    /// <summary>Reads current pairing, operator, signed policy and devices.</summary>
    /// <param name="token">Cancellation for the current read.</param>
    /// <returns>Current authenticated setup or a safe failure.</returns>
    Task<Result<StudioVerificationState, DesktopError>> ReadAsync(CancellationToken token);

    /// <summary>Pairs using an administrator-issued invitation.</summary>
    /// <param name="request">Administrator-issued invitation and pinned HTTPS server.</param>
    /// <param name="token">Cancellation for pairing.</param>
    /// <returns>The durable pairing outcome.</returns>
    Task<UnitResult<DesktopError>> PairAsync(PairingRequest request, CancellationToken token);

    /// <summary>Authenticates an existing protected operator identity.</summary>
    /// <param name="identity">Existing protected operator identity reference.</param>
    /// <param name="token">Cancellation for sign-in.</param>
    /// <returns>The operator authentication outcome.</returns>
    Task<UnitResult<DesktopError>> SignInAsync(string identity, CancellationToken token);

    /// <summary>Downloads and activates signed configuration using anti-rollback checks.</summary>
    /// <param name="token">Cancellation for synchronization.</param>
    /// <returns>The signed activation outcome.</returns>
    Task<UnitResult<DesktopError>> SynchronizeAsync(CancellationToken token);

    /// <summary>Clears the local operator session.</summary>
    /// <param name="token">Cancellation for this action.</param>
    /// <returns>The local session cleanup outcome.</returns>
    Task<UnitResult<DesktopError>> SignOutAsync(CancellationToken token);

    /// <summary>Withdraws pairing and protected cached trust.</summary>
    /// <param name="token">Cancellation for withdrawal.</param>
    /// <returns>The protected trust withdrawal outcome.</returns>
    Task<UnitResult<DesktopError>> UnpairAsync(CancellationToken token);

    /// <summary>Performs one current-authority ceremony, retaining owned secrets only until it drains.</summary>
    /// <param name="selection">Untrusted UI selections rechecked by the backend.</param>
    /// <param name="pin">Caller-owned explicit PIN.</param>
    /// <param name="privacyKey">Caller-owned TWIC key container.</param>
    /// <param name="choose">Explicit selection among authenticated enrolled fingers.</param>
    /// <param name="token">Cancellation that drains the retained session.</param>
    /// <returns>Separate verification checks or a safe failure.</returns>
    Task<Result<CredentialVerificationResult, DesktopError>> VerifyAsync(
        StudioVerificationSelection selection,
        ReadOnlyMemory<byte> pin,
        ReadOnlyMemory<byte> privacyKey,
        Func<
            ImmutableArray<GuidedFingerChoice>,
            CancellationToken,
            Task<Result<GuidedFingerChoice, DesktopError>>
        > choose,
        CancellationToken token
    );
}

/// <summary>Creates a separately owned read-only verification installation on demand.</summary>
public interface IStudioVerificationBackendFactory
{
    /// <summary>Acquires the installation and initializes its real verification-only database.</summary>
    /// <param name="token">Cancellation for initialization.</param>
    /// <returns>An exclusively owned runtime or a safe setup failure.</returns>
    Task<Result<IStudioVerificationBackend, DesktopError>> OpenAsync(CancellationToken token);
}
