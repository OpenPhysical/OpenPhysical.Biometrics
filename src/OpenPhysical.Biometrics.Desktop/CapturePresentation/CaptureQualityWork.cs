// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed class CaptureQualityWork(ICaptureProcessor processor)
{
    internal async Task<Result<CapturedFingerprint, DesktopError>> RunAsync(
        CaptureFrame frozen,
        FingerPosition finger,
        CaptureWorkRequest request,
        Maybe<Func<FingerprintPreprocessingArtifact, UnitResult<DesktopError>>> show,
        CancellationToken token
    )
    {
        var evaluated = await Task.Run(() =>
            request.Candidates.IsDefaultOrEmpty
                ? processor.EvaluateFrame(frozen, request.Processing, token)
                : processor.EvaluateBurst(
                    request.Candidates,
                    request.Processing,
                    request.Quality,
                    token
                )
        );
        if (evaluated.IsFailure)
            return evaluated.Error;
        var shown = Display(evaluated.Value.SelectedArtifact, show);
        if (shown.IsFailure)
            return shown.Error;
        var captured = await Task.Run(() =>
            processor.Extract(
                evaluated.Value,
                finger,
                Maybe<CaptureImageQualityEvidence>.None,
                token
            )
        );
        return captured.Map(value => request.OriginalAcquisitionPolicy.ApplyTo(value));
    }

    internal async Task<Result<CapturedFingerprint, DesktopError>> ReprocessAsync(
        FingerprintPreprocessingArtifact artifact,
        FingerPosition finger,
        CapturePolicySettings policy,
        Maybe<CaptureImageQualityEvidence> original,
        CaptureAdjustmentEvidence evidence,
        Func<CaptureAdjustmentPrepared, bool> show,
        CancellationToken token
    )
    {
        var prepared = PrepareRevision(artifact, policy, evidence, token);
        if (prepared.IsFailure)
            return prepared.Error;
        var selected = await Task.Run(() =>
            processor.EvaluateArtifact(prepared.Value, policy.Quality, token)
        );
        if (selected.IsFailure)
            return selected.Error;
        var current = CaptureProcessingCancellation.Check(token);
        if (current.IsFailure)
            return current.Error;
        if (
            !show(
                new CaptureAdjustmentPrepared(
                    selected.Value.SelectedArtifact,
                    CaptureAdjustmentEvidence.From(selected.Value.Selected.Prepared)
                )
            )
        )
            return new DesktopError(
                "Capture.Presentation",
                "The final adjusted image cannot be displayed."
            );
        return await Task.Run(() => processor.Extract(selected.Value, finger, original, token));
    }

    private Result<CapturePreparedFrame, DesktopError> PrepareRevision(
        FingerprintPreprocessingArtifact artifact,
        CapturePolicySettings policy,
        CaptureAdjustmentEvidence evidence,
        CancellationToken token
    ) =>
        evidence.Contour.HasValue
        && evidence.ProcessingRegion.HasValue
        && evidence.Diagnostics.HasValue
            ? evidence
                .Input.Resolve(artifact.OriginalFrame, evidence.Contour.Value)
                .Map(
                    (Maybe<Fs50WholeContactProof> proof) =>
                        new CapturePreparedFrame(
                            artifact,
                            evidence.Diagnostics.Value,
                            evidence.Contour.Value,
                            evidence.ProcessingRegion.Value,
                            evidence.Input.Role,
                            proof
                        )
                        {
                            CachedAssessment = evidence.Assessment,
                            CachedExtraction = evidence.Extraction,
                            CachedOriginalAssessment = evidence.OriginalAssessment,
                            CachedOriginalExtraction = evidence.OriginalExtraction,
                        }
                )
                .Check(CapturePreparedValidation.Check)
            : processor.PrepareFrame(
                artifact.OriginalFrame,
                new(
                    artifact.Policy,
                    policy.Evidence,
                    policy.Contour,
                    artifact.ProcessingRevision,
                    evidence.Input
                ),
                token
            );

    private static UnitResult<DesktopError> Display(
        FingerprintPreprocessingArtifact artifact,
        Maybe<Func<FingerprintPreprocessingArtifact, UnitResult<DesktopError>>> show
    ) =>
        show.Match(
            present => present(artifact),
            () =>
                UnitResult.Failure(
                    new DesktopError(
                        "Capture.Presentation",
                        "The processing image cannot be displayed."
                    )
                )
        );
}
