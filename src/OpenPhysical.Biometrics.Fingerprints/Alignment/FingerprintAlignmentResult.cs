// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using System.Collections.Immutable;

/// <summary>Contains geometric registration evidence, independently of biometric match scores.</summary>
/// <param name="Transform">Bounded similarity transform in normalized physical coordinates.</param>
/// <param name="Pairs">Unique reference/probe point correspondences.</param>
/// <param name="RootMeanSquareError">Correspondence residual at 197 pixels per centimetre.</param>
/// <param name="ReferenceMinutiae">All reference points transformed into probe pixel coordinates.</param>
public sealed record FingerprintAlignmentResult(
    FingerprintSimilarityTransform Transform,
    ImmutableArray<FingerprintAlignmentPair> Pairs,
    double RootMeanSquareError,
    ImmutableArray<FingerprintAlignedMinutia> ReferenceMinutiae
);
