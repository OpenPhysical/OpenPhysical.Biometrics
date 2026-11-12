// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed class CaptureCandidateScoring(CaptureImageScoring images)
{
    internal Result<ImmutableArray<CaptureCandidateEvaluation>, DesktopError> EvaluateBurst(
        ImmutableArray<FingerCaptureCandidate> candidates,
        CaptureProcessingRequest request,
        CancellationToken token
    )
    {
        if (candidates.IsDefaultOrEmpty)
            return new DesktopError(
                "Capture.InvalidBurst",
                "A frozen burst requires acknowledged acquisition images."
            );
        var evaluated = ImmutableArray.CreateBuilder<CaptureCandidateEvaluation>(candidates.Length);
        foreach (var candidate in candidates)
        {
            var result = CaptureFramePreparation
                .Prepare(candidate.Frame, request, token)
                .Bind(prepared => Assess(prepared, token));
            if (result.IsFailure)
                return result.Error;
            evaluated.Add(result.Value with { Source = candidate });
        }
        return evaluated.MoveToImmutable();
    }

    internal Result<CaptureCandidateEvaluation, DesktopError> Assess(
        CapturePreparedFrame prepared,
        CancellationToken token
    ) =>
        CaptureProcessingCancellation
            .Check(token)
            .Bind(() => CapturePreparedValidation.Check(prepared))
            .Bind(() => Score(prepared, token))
            .Map(scored => new CaptureCandidateEvaluation(default, prepared, scored));

    private Result<ScoredFingerprintImage, DesktopError> Score(
        CapturePreparedFrame prepared,
        CancellationToken token
    ) =>
        images
            .ReuseProcessed(prepared)
            .Match(
                value => Result.Success<ScoredFingerprintImage, DesktopError>(value),
                () =>
                    images.Assess(
                        prepared.Artifact.Pixels,
                        prepared.Artifact.Width,
                        prepared.Artifact.Height,
                        prepared.Artifact.ProcessedDigest,
                        CaptureImageRole.Processed,
                        token
                    )
            );
}
