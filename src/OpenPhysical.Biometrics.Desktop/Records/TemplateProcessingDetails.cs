// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

/// <summary>Verified original, processed, quality, source, and policy evidence for a saved view.</summary>
/// <param name="Original">The verified original image.</param>
/// <param name="OriginalAssessment">The original image quality evidence.</param>
/// <param name="ProcessedAssessment">The processed image quality evidence.</param>
/// <param name="Mask">The exact ROI and artificial-cut guard used for processing.</param>
/// <param name="Policy">The validated policy parameters.</param>
/// <param name="SelectionPolicy">The validated candidate selection parameters.</param>
/// <param name="SelectionReason">The recorded reason for selecting this candidate.</param>
/// <param name="Candidates">Immutable candidate summaries without unselected image pixels.</param>
/// <param name="Source">The original acquisition identity.</param>
/// <param name="Version">The algorithm identifier.</param>
/// <param name="ProcessingRevision">The processing settings revision.</param>
/// <param name="Identity">The source, pixel, mask, and policy SHA-256 binding.</param>
public sealed record TemplateProcessingDetails(
    TemplateImage Original,
    CaptureImageQualityEvidence OriginalAssessment,
    CaptureImageQualityEvidence ProcessedAssessment,
    FingerprintProcessingMask Mask,
    FingerprintPreprocessingPolicy Policy,
    FingerprintQualitySelectionPolicy SelectionPolicy,
    CaptureSelectionReason SelectionReason,
    ImmutableArray<CaptureCandidateSummary> Candidates,
    FingerprintProcessingSource Source,
    string Version,
    long ProcessingRevision,
    string Identity
)
{
    /// <summary>Gets the verified contact-stage summary and optional image planes.</summary>
    public Maybe<TemplateContactDetails> ContactDiagnostics { get; init; }

    /// <summary>Gets the acquisition settings snapshot when supplied.</summary>
    public Maybe<CaptureAcquisitionPolicy> OriginalAcquisitionPolicy { get; init; }

    /// <summary>Gets the measured contact, inferred holes, feature region, and processing context.</summary>
    public Maybe<Fs50ContactContourArtifact> Contour { get; init; }

    /// <summary>Gets all observed selected weak texture used to reject unsupported exterior.</summary>
    public ImmutableArray<byte> ObservedContactMask { get; init; } = [];

    /// <summary>Gets the actual guarded context and eligible feature mask used for extraction.</summary>
    public Maybe<Fs50ContourProcessingRegion> ProcessingRegion { get; init; }

    /// <summary>Gets whether the image was a live sensor acquisition or a declared offline input.</summary>
    public CaptureProcessingInputRole InputRole { get; init; }

    /// <summary>Gets recorded original qualification metadata, which grants no live authorization.</summary>
    public Maybe<Fs50WholeContactRecord> OriginalQualification { get; init; }
}
