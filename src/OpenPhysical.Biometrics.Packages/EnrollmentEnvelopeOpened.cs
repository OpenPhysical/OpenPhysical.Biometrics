// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

/// <summary>A fully authenticated envelope and stable origin idempotency key.</summary>
public sealed record EnrollmentEnvelopeOpened
{
    /// <summary>Creates a complete result object.</summary>
    /// <param name="transfer">Authenticated transfer with all artifacts.</param>
    /// <param name="idempotencyDigest">Stable origin statement digest.</param>
    public EnrollmentEnvelopeOpened(EnrollmentEnvelopeTransfer transfer, string idempotencyDigest)
    {
        Transfer = transfer;
        IdempotencyDigest = idempotencyDigest;
    }

    /// <summary>Gets the authenticated complete transfer.</summary>
    public EnrollmentEnvelopeTransfer Transfer { get; }

    /// <summary>Gets the stable origin idempotency digest.</summary>
    public string IdempotencyDigest { get; }
}
