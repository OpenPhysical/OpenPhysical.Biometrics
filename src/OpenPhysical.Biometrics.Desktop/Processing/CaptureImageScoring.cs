// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

internal sealed class CaptureImageScoring(
    FingerprintPreparationService preparation,
    IFingerprintUnifiedQualityProvider unifiedQuality
)
{
    private readonly Guid qualityOwner = Guid.NewGuid();

    internal Maybe<ScoredFingerprintImage> ReuseOriginal(CaptureCandidateEvaluation candidate) =>
        CaptureQualityReuse.Original(candidate, qualityOwner);

    internal bool Owns(ScoredFingerprintImage scored) =>
        CaptureQualityReuse.Owned(scored, qualityOwner);

    internal Maybe<ScoredFingerprintImage> ReuseProcessed(CapturePreparedFrame prepared) =>
        prepared
            .CachedAssessment.Bind(scored =>
                CaptureQualityReuse.Processed(scored, prepared.Artifact, qualityOwner)
            )
            .Or(
                prepared.CachedOriginalAssessment.Bind(scored =>
                    CaptureQualityReuse.Processed(scored, prepared.Artifact, qualityOwner)
                )
            );

    internal Result<ScoredFingerprintImage, DesktopError> Assess(
        ImmutableArray<byte> pixels,
        int width,
        int height,
        string digest,
        CaptureImageRole role,
        CancellationToken token
    ) =>
        CaptureProcessingCancellation
            .Check(token)
            .Bind(() =>
                DesktopEdge.Try(
                    () => new FingerprintImage(pixels.AsSpan(), width, height, 500),
                    "Capture.Image",
                    "Cannot prepare the exact image for quality assessment."
                )
            )
            .Bind(image =>
                preparation
                    .Assess(image)
                    .MapError(error => new DesktopError(error.Code, error.Message))
            )
            .Bind(assessed => AssessUnified(assessed, digest, role, token));

    private Result<ScoredFingerprintImage, DesktopError> AssessUnified(
        AssessedFingerprint assessed,
        string digest,
        CaptureImageRole role,
        CancellationToken token
    )
    {
        var active = CaptureProcessingCancellation.Check(token);
        if (active.IsFailure)
            return active.Error;
        var unified = unifiedQuality.Assess(assessed.Image);
        active = CaptureProcessingCancellation.Check(token);
        if (active.IsFailure)
            return active.Error;
        if (
            unified.IsFailure
            && unified.Error.Code != FingerprintErrorCodes.QualityImageNotAssessable
        )
            return new DesktopError(unified.Error.Code, unified.Error.Message);
        var diagnostics = Diagnostics(assessed);
        if (unified.IsFailure)
            diagnostics = diagnostics.Add(
                new DesktopError(unified.Error.Code, unified.Error.Message)
            );
        var quality = unified.IsSuccess
            ? Maybe.From(unified.Value)
            : Maybe<FingerprintUnifiedQualityAssessment>.None;
        return new ScoredFingerprintImage(
            assessed,
            new(digest, role, assessed.Quality, quality, diagnostics)
        )
        {
            QualityOwner = qualityOwner,
            NativeUnifiedAssessment = quality,
        };
    }

    private static ImmutableArray<DesktopError> Diagnostics(AssessedFingerprint assessed) =>
        assessed.Quality.Status == FingerprintQualityStatus.Assessed
            ? []
            :
            [
                new(
                    "Capture.Nfiq1NotAssessable",
                    "NFIQ 1 could not assess usable fingerprint detail."
                ),
            ];
}
