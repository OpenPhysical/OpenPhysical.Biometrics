// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using System.Collections.Immutable;

/// <summary>An immutable unsigned INCITS record and its ordered view lineage.</summary>
public sealed class PreparedFingerprintRecord
{
    internal PreparedFingerprintRecord(
        ImmutableArray<byte> incits378,
        ImmutableArray<FingerprintPreparationEvidence> evidence
    )
    {
        Incits378 = incits378;
        Evidence = evidence;
    }

    /// <summary>Gets the canonical INCITS 378-2004 bytes without CBEFF or a signature.</summary>
    public ImmutableArray<byte> Incits378 { get; }

    /// <summary>Gets one lineage entry for each view, in record order.</summary>
    public ImmutableArray<FingerprintPreparationEvidence> Evidence { get; }
}
