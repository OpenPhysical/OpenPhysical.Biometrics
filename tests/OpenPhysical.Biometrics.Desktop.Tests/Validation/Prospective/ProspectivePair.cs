// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal sealed record ProspectivePair(
    string LeftId,
    string RightId,
    bool Genuine,
    bool BetweenPerson,
    ImmutableArray<ProspectiveScore> Scores
)
{
    internal Maybe<int> Score(ProspectiveArm arm) => Scores.Single(value => value.Arm == arm).Score;
}

internal sealed record ProspectiveScore(ProspectiveArm Arm, Maybe<int> Score);
