// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;

internal sealed record ProspectiveReport(
    string Cohort,
    string FrozenProtocolDigest,
    string CohortManifestDigest,
    ImmutableArray<ProspectiveImage> Images,
    ImmutableArray<ProspectivePair> Pairs,
    ImmutableArray<ProspectiveLedger> Ledgers,
    int Nfiq1Calls,
    int Nfiq2Calls,
    int Extractions
);
