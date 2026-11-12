// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using CSharpFunctionalExtensions;

internal sealed record ProspectiveLedgerPair(
    string LeftId,
    string RightId,
    Maybe<int> OriginalScore,
    Maybe<int> ProcessedScore
)
{
    public bool OriginalAccepted => OriginalScore.HasValue && OriginalScore.Value >= 40;
    public bool ProcessedAccepted => ProcessedScore.HasValue && ProcessedScore.Value >= 40;
}
