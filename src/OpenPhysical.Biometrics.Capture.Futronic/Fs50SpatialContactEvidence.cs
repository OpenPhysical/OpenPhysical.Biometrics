// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal sealed record Fs50SpatialContactEvidence(
    ImmutableArray<byte> Contact,
    ImmutableArray<byte> Observed,
    int ComponentCount,
    Maybe<Fs50RegionStatistics> Statistics,
    Fs50ContactIssue Issue,
    int RidgeExteriorArea,
    int LargestRidgeExteriorArea
);
