// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;

/// <summary>A selected frozen image, its baseline, and the complete bounded scoring evidence.</summary>
/// <param name="Selected">The selected evaluated acquisition.</param>
/// <param name="Baseline">The usable motion-ranked baseline.</param>
/// <param name="Candidates">Complete bounded evaluation set.</param>
/// <param name="Policy">The fixed independent quality-gain policy.</param>
/// <param name="Reason">The deterministic selection reason.</param>
public sealed record CaptureBurstSelection(
    CaptureCandidateEvaluation Selected,
    CaptureCandidateEvaluation Baseline,
    ImmutableArray<CaptureCandidateEvaluation> Candidates,
    FingerprintQualitySelectionPolicy Policy,
    CaptureSelectionReason Reason
)
{
    /// <summary>Gets the exact selected processing artifact.</summary>
    public FingerprintPreprocessingArtifact SelectedArtifact => Selected.Artifact;
}
