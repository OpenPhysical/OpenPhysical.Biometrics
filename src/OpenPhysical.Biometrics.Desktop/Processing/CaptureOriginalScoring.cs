// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal sealed class CaptureOriginalScoring(CaptureImageScoring images)
{
    internal Result<CaptureOriginalAssessment, DesktopError> Resolve(
        CaptureCandidateEvaluation candidate,
        Maybe<CaptureImageQualityEvidence> existing,
        CancellationToken token
    )
    {
        var artifact = candidate.Artifact;
        var cached = images.ReuseOriginal(candidate);
        if (existing.HasValue)
            return
                existing.Value.Role == CaptureImageRole.Original
                && existing.Value.ImageDigest == artifact.OriginalDigest
                ? new CaptureOriginalAssessment(
                    existing.Value,
                    cached,
                    candidate.Prepared.CachedOriginalExtraction
                )
                : new DesktopError(
                    "Capture.QualityEvidenceMismatch",
                    "Original quality evidence does not belong to this acquisition."
                );
        if (cached.HasValue)
            return new CaptureOriginalAssessment(
                cached.Value.Evidence,
                cached,
                candidate.Prepared.CachedOriginalExtraction
            );
        return images
            .Assess(
                artifact.OriginalFrame.Pixels,
                artifact.Width,
                artifact.Height,
                artifact.OriginalDigest,
                CaptureImageRole.Original,
                token
            )
            .Map(scored => new CaptureOriginalAssessment(scored.Evidence, scored, default));
    }
}
