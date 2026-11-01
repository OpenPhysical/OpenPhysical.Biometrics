// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;

/// <summary>Supplies CMS construction and cryptographic signing on behalf of the caller.</summary>
public interface IBiometricSignatureProvider
{
    /// <summary>Produces an owned detached CMS block for the exact requested content.</summary>
    /// <param name="request">Content and biometric-specific signed-attribute requirements.</param>
    /// <param name="cancellationToken">Cancellation for external signing operations.</param>
    /// <returns>The complete detached CMS block.</returns>
    /// <remarks>The signature workflow converts any exception from this call, including cancellation,
    /// into a failed result with a stable error code.</remarks>
    Task<ImmutableArray<byte>> SignAsync(
        BiometricSignatureRequest request,
        CancellationToken cancellationToken = default
    );
}
