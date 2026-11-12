// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed record TemplateProcessingValidated(
    TemplateProcessingManifest Manifest,
    FingerprintPreprocessingPolicy Policy,
    FingerprintQualitySelectionPolicy SelectionPolicy,
    FingerprintProcessingSource Source,
    CaptureImageQualityEvidence OriginalAssessment,
    CaptureImageQualityEvidence ProcessedAssessment,
    CaptureSelectionReason SelectionReason,
    ImmutableArray<CaptureCandidateSummary> Candidates
)
{
    public Maybe<CaptureAcquisitionPolicy> OriginalAcquisitionPolicy { get; init; }
}
