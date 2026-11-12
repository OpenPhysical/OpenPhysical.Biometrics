// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

/// <summary>Prepares frozen fingerprint images, assesses candidates, and extracts one selected image.</summary>
public sealed class CaptureProcessor : ICaptureProcessor
{
    private readonly CaptureCandidateScoring scoring;
    private readonly CaptureCandidateExtractor extractor;

    /// <summary>Creates a reusable processor from singleton native-library adapters.</summary>
    /// <param name="preparation">Shared extraction and NFIQ 1 service.</param>
    /// <param name="unifiedQuality">Shared NFIQ 2 assessment service.</param>
    public CaptureProcessor(
        FingerprintPreparationService preparation,
        IFingerprintUnifiedQualityProvider unifiedQuality
    )
    {
        var images = new CaptureImageScoring(preparation, unifiedQuality);
        scoring = new(images);
        extractor = new(preparation, images);
    }

    /// <inheritdoc/>
    public Result<CapturePreparedFrame, DesktopError> PrepareFrame(
        CaptureFrame original,
        CaptureProcessingRequest request,
        CancellationToken token
    ) => CaptureFramePreparation.Prepare(original, request, token);

    /// <inheritdoc/>
    public Result<CaptureCandidateEvaluation, DesktopError> Assess(
        CapturePreparedFrame prepared,
        CancellationToken token
    ) => scoring.Assess(prepared, token);

    /// <inheritdoc/>
    public Result<CaptureBurstSelection, DesktopError> EvaluateFrame(
        CaptureFrame original,
        CaptureProcessingRequest request,
        CancellationToken token
    ) =>
        PrepareFrame(original, request, token)
            .Bind(prepared =>
                EvaluateArtifact(prepared, FingerprintQualitySelectionPolicy.Default, token)
            );

    /// <inheritdoc/>
    public Result<CaptureBurstSelection, DesktopError> EvaluateBurst(
        ImmutableArray<FingerCaptureCandidate> candidates,
        CaptureProcessingRequest request,
        FingerprintQualitySelectionPolicy selection,
        CancellationToken token
    ) =>
        scoring
            .EvaluateBurst(candidates, request, token)
            .Bind(evaluated => CaptureSelectionFactory.Choose(evaluated, selection));

    /// <inheritdoc/>
    public Result<CaptureBurstSelection, DesktopError> EvaluateArtifact(
        CapturePreparedFrame prepared,
        FingerprintQualitySelectionPolicy selection,
        CancellationToken token
    ) =>
        Assess(prepared, token)
            .Bind(evaluated => CaptureSelectionFactory.Choose([evaluated], selection))
            .Map(selected => selected with { Reason = CaptureSelectionReason.Manual });

    /// <inheritdoc/>
    public Result<PreparedFingerprint, DesktopError> ExtractPrepared(
        CaptureCandidateEvaluation candidate,
        CancellationToken token
    ) => extractor.ExtractPrepared(candidate, token);

    /// <inheritdoc/>
    public Result<CapturedFingerprint, DesktopError> CompleteCapture(
        CaptureBurstSelection selection,
        PreparedFingerprint prepared,
        FingerPosition finger,
        Maybe<CaptureImageQualityEvidence> originalAssessment,
        CancellationToken token
    ) => extractor.Complete(selection, prepared, finger, originalAssessment, token);

    /// <inheritdoc/>
    public Result<CapturedFingerprint, DesktopError> Extract(
        CaptureBurstSelection selection,
        FingerPosition finger,
        Maybe<CaptureImageQualityEvidence> originalAssessment,
        CancellationToken token
    ) => extractor.Extract(selection, finger, originalAssessment, token);
}
