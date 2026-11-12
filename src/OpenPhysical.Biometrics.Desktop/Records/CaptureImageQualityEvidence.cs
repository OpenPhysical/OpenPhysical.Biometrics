// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

/// <summary>Actual source-bound NFIQ assessments and explicit image diagnostics.</summary>
/// <param name="ImageDigest">Digest of the exact assessed pixels.</param>
/// <param name="Role">Original or processed image role.</param>
/// <param name="Nfiq1">Actual NFIQ 1 assessment with status and version.</param>
/// <param name="Nfiq2">Actual NFIQ 2 assessment when assessable.</param>
/// <param name="Diagnostics">Explicit native image diagnostics.</param>
public sealed record CaptureImageQualityEvidence(
    string ImageDigest,
    CaptureImageRole Role,
    FingerprintQualityAssessment Nfiq1,
    Maybe<FingerprintUnifiedQualityAssessment> Nfiq2,
    ImmutableArray<DesktopError> Diagnostics
)
{
    /// <summary>Gets whether both actual quality assessments completed successfully.</summary>
    public bool IsAssessed =>
        Nfiq1.Status == FingerprintQualityStatus.Assessed && Nfiq2.HasValue && Diagnostics.IsEmpty;
}
