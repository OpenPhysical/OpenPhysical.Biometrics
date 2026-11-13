// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

// Replans source-supported geometry without running inference again.
internal static class PortraitPreparation
{
    internal static Result<PortraitPreparationResult, FacialError> Prepare(
        PortraitAnalysis analysis,
        Maybe<ReviewedPortraitGeometry> geometry,
        Maybe<int> faceIndex,
        PortraitMouthPolicy mouthPolicy,
        ILogger logger
    )
    {
        var source = new PortraitSource(analysis.SourceSha256, analysis.Image.Pixels);
        return ReviewedGeometryCheck
            .Resolve(geometry, source, faceIndex)
            .Bind(index => Complete(source, geometry, index, analysis.Faces, mouthPolicy, logger));
    }

    private static Result<PortraitPreparationResult, FacialError> Complete(
        PortraitSource source,
        Maybe<ReviewedPortraitGeometry> geometry,
        Maybe<int> index,
        ImmutableArray<PortraitFace> faces,
        PortraitMouthPolicy mouthPolicy,
        ILogger logger
    )
    {
        var review = PortraitFaceSelector.NeedsReview(faces, index);
        return review.HasValue
            ? PortraitOutcomes.Review(source, faces, review.Value).Succeed()
            : PortraitFaceSelector
                .Select(faces, index.GetValueOrDefault(0))
                .Bind(face => Crop(source, geometry, faces, face, mouthPolicy, logger));
    }

    private static Result<PortraitPreparationResult, FacialError> Crop(
        PortraitSource source,
        Maybe<ReviewedPortraitGeometry> geometry,
        ImmutableArray<PortraitFace> faces,
        PortraitFace face,
        PortraitMouthPolicy mouthPolicy,
        ILogger logger
    ) =>
        PortraitFeatures
            .FromMediaPipe(face.Landmarks)
            .Bind(features =>
            {
                var image = source.Image;
                var checks = PortraitCaptureChecks.Assess(face, features, mouthPolicy);
                var crop = PortraitFraming.Calculate(
                    face,
                    features,
                    geometry,
                    image.Width,
                    image.Height
                );
                return (
                    crop.IsFailure
                        ? PortraitOutcomes
                            .Review(source, faces, new(PortraitStatus.ReviewRequired, crop.Error))
                            .MapFindings(checks.Findings)
                            .Succeed()
                        : Warp(source, faces, crop.Value, checks.Findings, logger)
                ).Map(prepared =>
                    prepared with
                    {
                        MouthPolicy = mouthPolicy,
                        MouthMeasurement = checks.MouthMeasurement,
                    }
                );
            });

    private static Result<PortraitPreparationResult, FacialError> Warp(
        PortraitSource source,
        ImmutableArray<PortraitFace> faces,
        PortraitCropPlan plan,
        ImmutableArray<PortraitFinding> findings,
        ILogger logger
    ) =>
        source
            .Image.Warp(PortraitOutput.Width, PortraitOutput.Height, plan.Transform)
            .Map(pixels =>
            {
                var prepared = PortraitOutcomes
                    .Prepared(source, faces, plan, pixels)
                    .MapFindings(findings);
                logger.LogDebug(
                    "Portrait preparation completed with {FindingCount} findings.",
                    prepared.Findings.Length
                );
                return prepared;
            });
}
