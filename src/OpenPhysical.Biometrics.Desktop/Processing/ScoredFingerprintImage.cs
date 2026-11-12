// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using OpenPhysical.Biometrics.Fingerprints;

/// <summary>Reusable assessment of exact immutable pixels with its source identity.</summary>
/// <param name="Assessment">Exact immutable image and its NFIQ 1 assessment.</param>
/// <param name="Evidence">Source-bound NFIQ assessments and diagnostics.</param>
public sealed record ScoredFingerprintImage(
    AssessedFingerprint Assessment,
    CaptureImageQualityEvidence Evidence
)
{
    internal Guid QualityOwner { get; init; }
    internal CSharpFunctionalExtensions.Maybe<FingerprintUnifiedQualityAssessment> NativeUnifiedAssessment { get; init; }
}
