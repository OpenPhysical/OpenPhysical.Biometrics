// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

/// <summary>Immutable acquisition and quality summary without an unselected image copy.</summary>
/// <param name="OriginalDigest">Digest of the unmodified source pixels.</param>
/// <param name="ProcessedDigest">Digest of the actual assessed processing pixels.</param>
/// <param name="Sequence">Original acquisition sequence.</param>
/// <param name="CapturedAt">Original acquisition timestamp.</param>
/// <param name="Quality">Actual quality assessments and diagnostics.</param>
/// <param name="Motion">Independent motion measurement when available.</param>
/// <param name="ClippedFraction">Independent clipping fraction when available.</param>
/// <param name="ReliableCoverage">Measured supported area, excluding uncertain expansion.</param>
/// <param name="ObservedAt">Monotonic presentation timestamp when available.</param>
public sealed record CaptureCandidateSummary(
    string OriginalDigest,
    string ProcessedDigest,
    long Sequence,
    DateTimeOffset CapturedAt,
    CaptureImageQualityEvidence Quality,
    Maybe<double> Motion,
    Maybe<double> ClippedFraction,
    int ReliableCoverage,
    Maybe<long> ObservedAt
);
