// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

/// <summary>An exact processed image with its mandatory source-bound contact, feature and context evidence.</summary>
/// <param name="Artifact">Exact original and processing pixels.</param>
/// <param name="Diagnostics">Independent original contact evidence.</param>
/// <param name="Contour">Original contact, inferred holes and independent context envelope.</param>
/// <param name="ProcessingRegion">Exact context cut guard and eligible feature mask.</param>
/// <param name="InputRole">Declared offline input or physical sensor acquisition.</param>
/// <param name="WholeContactProof">Original physical qualification, absent for offline images.</param>
public sealed record CapturePreparedFrame(
    FingerprintPreprocessingArtifact Artifact,
    Fs50ContactDiagnostics Diagnostics,
    Fs50ContactContourArtifact Contour,
    Fs50ContourProcessingRegion ProcessingRegion,
    CaptureProcessingInputRole InputRole,
    Maybe<Fs50WholeContactProof> WholeContactProof
)
{
    internal Maybe<ScoredFingerprintImage> CachedAssessment { get; init; }
    internal Maybe<PreparedFingerprint> CachedExtraction { get; init; }
    internal Maybe<ScoredFingerprintImage> CachedOriginalAssessment { get; init; }
    internal Maybe<PreparedFingerprint> CachedOriginalExtraction { get; init; }
}
