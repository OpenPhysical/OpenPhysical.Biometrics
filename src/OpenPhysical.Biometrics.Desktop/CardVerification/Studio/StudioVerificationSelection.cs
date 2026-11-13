// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using OpenPhysical.Middleware.Domain.Piv;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Untrusted UI selections rechecked against the current signed configuration.</summary>
/// <param name="ReaderId">Host-issued reader selection.</param>
/// <param name="InterfaceType">Physical interface requested by the operator.</param>
/// <param name="Choice">Exact ceremony selection.</param>
/// <param name="ScannerId">Host-discovered scanner identity.</param>
/// <param name="MarsecLevel">Explicit TWIC maritime security level.</param>
/// <param name="IssuerCertificates">Untrusted path-building candidates.</param>
/// <param name="ContentSignerCertificates">Untrusted content-signer candidates.</param>
public sealed record StudioVerificationSelection(
    Guid ReaderId,
    PivInterfaceType InterfaceType,
    StudioVerificationChoice Choice,
    string ScannerId,
    int MarsecLevel,
    ImmutableArray<ImmutableArray<byte>> IssuerCertificates,
    ImmutableArray<ImmutableArray<byte>> ContentSignerCertificates
);
