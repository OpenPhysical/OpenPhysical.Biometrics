// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

// Builds the owned preparation results for review and prepared outcomes.
internal static class PortraitOutcomes
{
    internal static PortraitPreparationResult Review(
        PortraitSource source,
        ImmutableArray<PortraitFace> faces,
        PortraitReview review
    ) =>
        new(
            review.Status,
            source.Hash,
            source.Image.Width,
            source.Image.Height,
            faces,
            Maybe<PortraitTransform>.None,
            Maybe<PortraitMeasurements>.None,
            [],
            [review.Finding]
        );

    internal static PortraitPreparationResult Prepared(
        PortraitSource source,
        ImmutableArray<PortraitFace> faces,
        PortraitCropPlan plan,
        byte[] pixels
    ) =>
        new(
            PortraitStatus.Prepared,
            source.Hash,
            source.Image.Width,
            source.Image.Height,
            faces,
            plan.Transform,
            plan.Measurements,
            ImmutableArray.Create(pixels),
            plan.Measurements.ReviewedHeadBoundary
                ? ImmutableArray<PortraitFinding>.Empty
                :
                [
                    new PortraitFinding(
                        "EstimatedHeadBoundary",
                        "Crown and ear geometry includes automatic estimates; review against the original capture."
                    ),
                ]
        );
}
