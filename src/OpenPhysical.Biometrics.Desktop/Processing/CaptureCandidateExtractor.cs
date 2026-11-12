// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed class CaptureCandidateExtractor(
    FingerprintPreparationService preparation,
    CaptureImageScoring images
)
{
    private readonly CaptureOriginalScoring originals = new(images);

    internal Result<PreparedFingerprint, DesktopError> ExtractPrepared(
        CaptureCandidateEvaluation candidate,
        CancellationToken token
    ) =>
        CaptureProcessingCancellation
            .Check(token)
            .Bind(() => CapturePreparedValidation.Check(candidate.Prepared))
            .Bind(() =>
                candidate.Processed.Evidence.IsAssessed
                    ? UnitResult.Success<DesktopError>()
                    : UnitResult.Failure(CaptureQualityFailure.From(candidate.Processed.Evidence))
            )
            .Ensure(
                () => Bound(candidate),
                new DesktopError(
                    "Capture.QualityEvidenceMismatch",
                    "The native assessment belongs to a different processing image."
                )
            )
            .Bind(() => ExtractOrReuse(candidate))
            .Check(_ => CaptureProcessingCancellation.Check(token));

    internal Result<CapturedFingerprint, DesktopError> Extract(
        CaptureBurstSelection selection,
        FingerPosition finger,
        Maybe<CaptureImageQualityEvidence> originalAssessment,
        CancellationToken token
    ) =>
        originals
            .Resolve(selection.Selected, originalAssessment, token)
            .Bind(original =>
                ExtractPrepared(selection.Selected, token)
                    .Bind(prepared =>
                        CompleteAssessed(selection, prepared, finger, original, token)
                    )
            );

    internal Result<CapturedFingerprint, DesktopError> Complete(
        CaptureBurstSelection selection,
        PreparedFingerprint prepared,
        FingerPosition finger,
        Maybe<CaptureImageQualityEvidence> originalAssessment,
        CancellationToken token
    ) =>
        CaptureProcessingCancellation
            .Check(token)
            .Bind(() => CapturePreparedValidation.Check(selection.Selected.Prepared))
            .Ensure(
                () => ExtractionBound(selection.Selected, prepared),
                new DesktopError(
                    "Capture.QualityEvidenceMismatch",
                    "The extraction does not belong to the selected assessed image."
                )
            )
            .Bind(() => originals.Resolve(selection.Selected, originalAssessment, token))
            .Bind(original => CompleteAssessed(selection, prepared, finger, original, token));

    private static Result<CapturedFingerprint, DesktopError> CompleteAssessed(
        CaptureBurstSelection selection,
        PreparedFingerprint prepared,
        FingerPosition finger,
        CaptureOriginalAssessment original,
        CancellationToken token
    ) =>
        CaptureProcessingCancellation
            .Check(token)
            .Bind(() =>
                CaptureViewFactory
                    .Create(
                        selection.SelectedArtifact.OriginalFrame,
                        finger,
                        prepared,
                        selection.SelectedArtifact.Mask.Inner,
                        default,
                        selection.Selected.Processed.Evidence.Nfiq2
                    )
                    .Map(capture =>
                        capture with
                        {
                            Processing = Evidence(
                                selection,
                                original.WithExtraction(prepared),
                                prepared
                            ),
                        }
                    )
            );

    private Result<PreparedFingerprint, DesktopError> ExtractOrReuse(
        CaptureCandidateEvaluation candidate
    )
    {
        var cached = candidate
            .Prepared.CachedExtraction.Bind(value =>
                ExtractionBound(candidate, value) ? Maybe.From(value) : default
            )
            .Or(
                candidate.Prepared.CachedOriginalExtraction.Bind(value =>
                    ExtractionBound(candidate, value) ? Maybe.From(value) : default
                )
            );
        if (images.Owns(candidate.Processed) && cached.HasValue)
            return cached.Value;
        return preparation
            .Extract(candidate.Processed.Assessment)
            .MapError(error => new DesktopError(error.Code, error.Message));
    }

    private static bool ExtractionBound(
        CaptureCandidateEvaluation candidate,
        PreparedFingerprint prepared
    ) =>
        Bound(candidate)
        && candidate.Processed.Evidence.IsAssessed
        && CaptureQualityReuse.Extracted(candidate.Processed, prepared);

    private static bool Bound(CaptureCandidateEvaluation candidate) =>
        candidate.Processed.Evidence.ImageDigest == candidate.Artifact.ProcessedDigest
        && candidate.Processed.Evidence.Role == CaptureImageRole.Processed
        && candidate.Processed.Assessment.Image.Width == candidate.Artifact.Width
        && candidate.Processed.Assessment.Image.Height == candidate.Artifact.Height
        && candidate.Processed.Assessment.Image.Pixels.SequenceEqual(
            candidate.Artifact.Pixels.AsSpan()
        );

    private static CaptureProcessingEvidence Evidence(
        CaptureBurstSelection selection,
        CaptureOriginalAssessment original,
        PreparedFingerprint prepared
    ) =>
        new(
            selection.SelectedArtifact,
            original.Evidence,
            selection.Policy,
            selection.Reason,
            selection.Candidates.Select(value => value.Summary).ToImmutableArray(),
            selection.Selected.ContactDiagnostics,
            selection.Selected.Contour,
            selection.Selected.ProcessingRegion,
            selection.Selected.Prepared.InputRole,
            selection.Selected.Prepared.WholeContactProof.Map(proof => proof.Record)
        )
        {
            RuntimeWholeContactProof = selection.Selected.Prepared.WholeContactProof,
            RuntimeAssessment = selection.Selected.Processed,
            RuntimeExtraction = prepared,
            RuntimeOriginalAssessment = original.Assessment,
            RuntimeOriginalExtraction = original.Extraction,
        };
}
