// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

/// <summary>Public staged workflow for frozen capture processing and reusable native assessments.</summary>
public interface ICaptureProcessor
{
    /// <summary>Prepares the final image and region only after acquisition has frozen and drained.</summary>
    /// <param name="original">Frozen unmodified source.</param>
    /// <param name="request">One immutable processing-policy snapshot.</param>
    /// <param name="token">Cancellation prevents further native calls.</param>
    /// <returns>Exact prepared image and evidence, or a typed failure.</returns>
    Result<CapturePreparedFrame, DesktopError> PrepareFrame(
        CaptureFrame original,
        CaptureProcessingRequest request,
        CancellationToken token
    );

    /// <summary>Assesses exact prepared pixels without extracting features or hiding image unassessability.</summary>
    /// <param name="prepared">Exact source-bound image and region evidence.</param>
    /// <param name="token">Cancellation prevents further native calls.</param>
    /// <returns>Actual assessments, including explicit image unassessability.</returns>
    Result<CaptureCandidateEvaluation, DesktopError> Assess(
        CapturePreparedFrame prepared,
        CancellationToken token
    );

    /// <summary>Prepares and selects one manual source without fabricated burst measurements.</summary>
    /// <param name="original">Frozen unmodified source.</param>
    /// <param name="request">One immutable processing-policy snapshot.</param>
    /// <param name="token">Cancellation prevents further native calls.</param>
    /// <returns>The manual selection, or its exact failure.</returns>
    Result<CaptureBurstSelection, DesktopError> EvaluateFrame(
        CaptureFrame original,
        CaptureProcessingRequest request,
        CancellationToken token
    );

    /// <summary>Assesses a bounded acknowledged burst under one policy, then selects a candidate.</summary>
    /// <param name="candidates">Frozen acknowledged source acquisitions.</param>
    /// <param name="request">One immutable processing-policy snapshot.</param>
    /// <param name="selection">Fixed meaningful quality gains.</param>
    /// <param name="token">Cancellation prevents further native calls.</param>
    /// <returns>The selected frozen candidate and bounded scoring evidence.</returns>
    Result<CaptureBurstSelection, DesktopError> EvaluateBurst(
        ImmutableArray<FingerCaptureCandidate> candidates,
        CaptureProcessingRequest request,
        FingerprintQualitySelectionPolicy selection,
        CancellationToken token
    );

    /// <summary>Assesses and selects an exact already displayed prepared image.</summary>
    /// <param name="prepared">Exact source-bound image and region evidence.</param>
    /// <param name="selection">Fixed meaningful quality gains.</param>
    /// <param name="token">Cancellation prevents further native calls.</param>
    /// <returns>The manual selection for these exact pixels.</returns>
    Result<CaptureBurstSelection, DesktopError> EvaluateArtifact(
        CapturePreparedFrame prepared,
        FingerprintQualitySelectionPolicy selection,
        CancellationToken token
    );

    /// <summary>Extracts native features once, reusing a source-bound successful assessment.</summary>
    /// <param name="candidate">A source-bound assessment of these exact pixels.</param>
    /// <param name="token">Cancellation prevents further native calls.</param>
    /// <returns>Native extracted features bound to the assessed image.</returns>
    Result<PreparedFingerprint, DesktopError> ExtractPrepared(
        CaptureCandidateEvaluation candidate,
        CancellationToken token
    );

    /// <summary>Completes a reusable extraction through the normal canonical capture projection.</summary>
    /// <param name="selection">Source-bound selection and processing evidence.</param>
    /// <param name="prepared">Unfiltered extraction of the selected assessed image.</param>
    /// <param name="finger">Operator-selected finger identity.</param>
    /// <param name="originalAssessment">Previously measured original quality when available.</param>
    /// <param name="token">Cancellation prevents further native calls.</param>
    /// <returns>The canonical capture or an extraction/evidence association failure.</returns>
    Result<CapturedFingerprint, DesktopError> CompleteCapture(
        CaptureBurstSelection selection,
        PreparedFingerprint prepared,
        FingerPosition finger,
        Maybe<CaptureImageQualityEvidence> originalAssessment,
        CancellationToken token
    );

    /// <summary>Extracts the winner and projects exactly the exported subset with complete evidence.</summary>
    /// <param name="selection">Fixed meaningful quality gains.</param>
    /// <param name="finger">Operator-selected finger identity.</param>
    /// <param name="originalAssessment">Previously measured original quality when available.</param>
    /// <param name="token">Cancellation prevents further native calls.</param>
    /// <returns>The canonical capture with complete current processing evidence.</returns>
    Result<CapturedFingerprint, DesktopError> Extract(
        CaptureBurstSelection selection,
        FingerPosition finger,
        Maybe<CaptureImageQualityEvidence> originalAssessment,
        CancellationToken token
    );
}
