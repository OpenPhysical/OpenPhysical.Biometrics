// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

internal sealed class EvaluationOriginalScoring(
    FingerprintPreparationService preparation,
    IFingerprintUnifiedQualityProvider unified
)
{
    internal Result<ScoredFingerprintImage, DesktopError> Assess(
        CaptureFrame frame,
        CancellationToken token
    ) =>
        EvaluationCancellation
            .Check(token)
            .Bind(() =>
                EvaluationEdge.Try(
                    () =>
                        new FingerprintImage(frame.Pixels.AsSpan(), frame.Width, frame.Height, 500),
                    "Evaluation.Image",
                    "Cannot prepare original control pixels."
                )
            )
            .Bind(image =>
                preparation
                    .Assess(image)
                    .MapError(error => new DesktopError(error.Code, error.Message))
            )
            .Bind(assessment => Unified(frame, assessment, token));

    private Result<ScoredFingerprintImage, DesktopError> Unified(
        CaptureFrame frame,
        AssessedFingerprint assessment,
        CancellationToken token
    )
    {
        var active = EvaluationCancellation.Check(token);
        if (active.IsFailure)
            return active.Error;
        var result = unified.Assess(assessment.Image);
        active = EvaluationCancellation.Check(token);
        if (active.IsFailure)
            return active.Error;
        if (
            result.IsFailure
            && result.Error.Code != FingerprintErrorCodes.QualityImageNotAssessable
        )
            return new DesktopError(result.Error.Code, result.Error.Message);
        var diagnostics = Diagnostics(assessment, result);
        return new ScoredFingerprintImage(
            assessment,
            new(
                EvaluationDigest.Of(frame.Pixels.ToArray()),
                CaptureImageRole.Original,
                assessment.Quality,
                result.Match(Maybe.From, _ => Maybe<FingerprintUnifiedQualityAssessment>.None),
                diagnostics
            )
        );
    }

    private static ImmutableArray<DesktopError> Diagnostics(
        AssessedFingerprint assessment,
        Result<FingerprintUnifiedQualityAssessment, FingerprintError> unified
    )
    {
        ImmutableArray<DesktopError> diagnostics =
            assessment.Quality.Status == FingerprintQualityStatus.Assessed
                ? []
                :
                [
                    new(
                        "Capture.Nfiq1NotAssessable",
                        "The original NFIQ 1 status is not assessed."
                    ),
                ];
        return unified.IsFailure
            ? diagnostics.Add(new(unified.Error.Code, unified.Error.Message))
            : diagnostics;
    }
}
