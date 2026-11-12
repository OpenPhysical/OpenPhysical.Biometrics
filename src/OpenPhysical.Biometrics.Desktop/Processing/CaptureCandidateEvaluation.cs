// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

/// <summary>A source-bound quality assessment without a native extraction.</summary>
/// <param name="Source">Acknowledged burst measurements, absent for a manual image.</param>
/// <param name="Prepared">Exact current image and region evidence.</param>
/// <param name="Processed">Actual assessments of these processing pixels.</param>
public sealed record CaptureCandidateEvaluation(
    Maybe<FingerCaptureCandidate> Source,
    CapturePreparedFrame Prepared,
    ScoredFingerprintImage Processed
)
{
    /// <summary>Gets the exact image artifact.</summary>
    public FingerprintPreprocessingArtifact Artifact => Prepared.Artifact;

    /// <summary>Gets independent source contact diagnostics.</summary>
    public Fs50ContactDiagnostics ContactDiagnostics => Prepared.Diagnostics;

    /// <summary>Gets the independent contact, inferred feature and context regions.</summary>
    public Fs50ContactContourArtifact Contour => Prepared.Contour;

    /// <summary>Gets the actual context cut guard and eligible feature mask.</summary>
    public Fs50ContourProcessingRegion ProcessingRegion => Prepared.ProcessingRegion;

    /// <summary>Gets measured ridge support, excluding uncertain added area.</summary>
    public int ReliableCoverage => Contour.Statistics.MeasuredSupportArea;
    internal CaptureCandidateSummary Summary =>
        new(
            Artifact.OriginalDigest,
            Artifact.ProcessedDigest,
            Artifact.OriginalFrame.Sequence,
            Artifact.OriginalFrame.CapturedAt,
            Processed.Evidence,
            Source.Bind(value =>
                double.IsFinite(value.Motion) ? Maybe.From(value.Motion) : Maybe<double>.None
            ),
            Source.Map(value => value.ClippedFraction),
            ReliableCoverage,
            Source.Map(value => value.ObservedAt)
        );
}
