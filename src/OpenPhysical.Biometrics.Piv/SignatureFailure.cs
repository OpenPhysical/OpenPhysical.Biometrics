// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

/// <summary>Identifies an external signing workflow failure.</summary>
public enum SignatureFailure
{
    /// <summary>The object was completed successfully.</summary>
    None,

    /// <summary>The provider returned an empty or oversized signature block.</summary>
    InvalidBlockLength,

    /// <summary>The signature length remained variable through the attempt limit.</summary>
    LengthDidNotConverge,

    /// <summary>The external verifier rejected the signature or its required envelope attributes.</summary>
    VerificationFailed,

    /// <summary>The authenticated identity differs from the requested identity.</summary>
    IdentityMismatch,
}
