// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

internal static class CaptureQualityReuse
{
    internal static Maybe<ScoredFingerprintImage> Original(
        CaptureCandidateEvaluation candidate,
        Guid owner
    ) =>
        Original(candidate.Processed, candidate.Artifact, owner)
            .Or(
                candidate.Prepared.CachedOriginalAssessment.Bind(scored =>
                    Original(scored, candidate.Artifact, owner)
                )
            );

    private static Maybe<ScoredFingerprintImage> Original(
        ScoredFingerprintImage scored,
        OpenPhysical.Biometrics.Capture.FingerprintPreprocessingArtifact artifact,
        Guid owner
    )
    {
        var original = artifact.OriginalFrame;
        if (!Owned(scored, owner))
            return Maybe<ScoredFingerprintImage>.None;
        if (
            scored.Evidence.ImageDigest != artifact.OriginalDigest
            || scored.Assessment.Image.Width != original.Width
            || scored.Assessment.Image.Height != original.Height
            || scored.Assessment.Image.DotsPerInch != 500
            || !scored.Assessment.Image.Pixels.SequenceEqual(original.Pixels.AsSpan())
        )
            return Maybe<ScoredFingerprintImage>.None;
        return scored with { Evidence = scored.Evidence with { Role = CaptureImageRole.Original } };
    }

    internal static bool Extracted(ScoredFingerprintImage scored, PreparedFingerprint prepared) =>
        ReferenceEquals(prepared.Image, scored.Assessment.Image)
        && ReferenceEquals(prepared.Quality, scored.Assessment.Quality)
        && prepared.Template.Width == scored.Assessment.Image.Width
        && prepared.Template.Height == scored.Assessment.Image.Height;

    internal static bool Owned(ScoredFingerprintImage scored, Guid owner) =>
        scored.QualityOwner == owner
        && scored.Evidence.IsAssessed
        && scored.NativeUnifiedAssessment.HasValue
        && scored.Evidence.Nfiq2.HasValue
        && ReferenceEquals(scored.NativeUnifiedAssessment.Value, scored.Evidence.Nfiq2.Value)
        && ReferenceEquals(scored.Assessment.Quality, scored.Evidence.Nfiq1);

    internal static Maybe<ScoredFingerprintImage> Processed(
        ScoredFingerprintImage scored,
        OpenPhysical.Biometrics.Capture.FingerprintPreprocessingArtifact artifact,
        Guid owner
    )
    {
        if (
            !Owned(scored, owner)
            || scored.Evidence.ImageDigest != artifact.ProcessedDigest
            || scored.Assessment.Image.Width != artifact.Width
            || scored.Assessment.Image.Height != artifact.Height
            || scored.Assessment.Image.DotsPerInch != 500
            || !scored.Assessment.Image.Pixels.SequenceEqual(artifact.Pixels.AsSpan())
        )
            return Maybe<ScoredFingerprintImage>.None;
        return scored with
        {
            Evidence = scored.Evidence with { Role = CaptureImageRole.Processed },
        };
    }
}
