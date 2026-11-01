// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;

/// <summary>Supplies CMS interpretation, signature verification, and configured trust evaluation.</summary>
public interface IBiometricSignatureVerifier
{
    /// <summary>Evaluates the supplied signature against the exact requested content.</summary>
    /// <param name="request">Original content and expected identity.</param>
    /// <param name="signatureBlock">The owned detached CMS block.</param>
    /// <param name="cancellationToken">Cancellation for external verification and trust operations.</param>
    /// <returns>Independent signature, envelope, trust, revocation, and authenticated identity findings.</returns>
    /// <remarks>The signature workflow converts any exception from this call, including cancellation,
    /// into a failed result with a stable error code.</remarks>
    Task<BiometricSignatureFindings> VerifyAsync(
        BiometricSignatureRequest request,
        ImmutableArray<byte> signatureBlock,
        CancellationToken cancellationToken = default
    );
}
