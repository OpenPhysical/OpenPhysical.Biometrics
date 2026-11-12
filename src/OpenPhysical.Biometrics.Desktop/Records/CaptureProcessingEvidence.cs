// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

/// <summary>Immutable processing and selection evidence retained without recomputation.</summary>
/// <param name="Artifact">Exact original and selected processing image.</param>
/// <param name="OriginalAssessment">Actual cached original image assessment.</param>
/// <param name="SelectionPolicy">Independent quality gain policy.</param>
/// <param name="SelectionReason">Reason for the selected acquisition.</param>
/// <param name="Candidates">Bounded assessment summaries, without unselected pixel copies.</param>
/// <param name="ContactDiagnostics">Independent original contact evidence.</param>
/// <param name="Contour">Original contact, inferred holes and current context envelope.</param>
/// <param name="ProcessingRegion">Actual context guard and eligible feature mask.</param>
/// <param name="InputRole">Declared offline input or physical sensor acquisition.</param>
/// <param name="OriginalQualification">Recorded original physical qualification, never a live capability.</param>
public sealed record CaptureProcessingEvidence(
    FingerprintPreprocessingArtifact Artifact,
    CaptureImageQualityEvidence OriginalAssessment,
    FingerprintQualitySelectionPolicy SelectionPolicy,
    CaptureSelectionReason SelectionReason,
    ImmutableArray<CaptureCandidateSummary> Candidates,
    Fs50ContactDiagnostics ContactDiagnostics,
    Fs50ContactContourArtifact Contour,
    Fs50ContourProcessingRegion ProcessingRegion,
    CaptureProcessingInputRole InputRole,
    Maybe<Fs50WholeContactRecord> OriginalQualification
)
{
    /// <summary>Gets the deterministic selector implementation identity.</summary>
    public string SelectionVersion => CaptureQualitySelection.Version;

    /// <summary>Gets the selected operator revision.</summary>
    public long ProcessingRevision => Artifact.ProcessingRevision;

    /// <summary>Gets the original acquisition-decision snapshot retained with a current Studio package.</summary>
    public Maybe<CaptureAcquisitionPolicy> OriginalAcquisitionPolicy { get; init; }

    internal Maybe<Fs50WholeContactProof> RuntimeWholeContactProof { get; init; }
    internal Maybe<ScoredFingerprintImage> RuntimeAssessment { get; init; }
    internal Maybe<PreparedFingerprint> RuntimeExtraction { get; init; }
    internal Maybe<ScoredFingerprintImage> RuntimeOriginalAssessment { get; init; }
    internal Maybe<PreparedFingerprint> RuntimeOriginalExtraction { get; init; }
}
