// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

internal sealed record Fs50WholeContactAnchor(
    Fs50EmptySensorKey Key,
    CaptureRegionGeometry Contact,
    CaptureRegionGeometry Feature,
    CaptureRegionGeometry Observed,
    CaptureRegionGeometry FilledObserved,
    string Digest
)
{
    internal static Result<Fs50WholeContactAnchor, CaptureError> Create(
        Fs50WholeContactInput input,
        Fs50WholeContactRecord record
    )
    {
        return new Fs50WholeContactAnchor(
            input.Key,
            input.Geometry.Contact,
            input.Geometry.Feature,
            input.ObservedGeometry.Contact,
            input.ObservedGeometry.Feature,
            Fs50PolicyDigest.Create(
                "fs50-whole-contact-anchor-v2",
                new
                {
                    record.Identity,
                    record.PolicyDigest,
                    record.RidgeEvidence.ObservedContactDigest,
                }
            )
        );
    }

    internal Result<
        (Fs50WholeContactSpatialComparison Spatial, bool Compatible),
        CaptureError
    > Evaluate(Fs50WholeContactInput input, Fs50WholeContactPolicy policy, CancellationToken token)
    {
        var dx = (int)Math.Round(input.Geometry.Contact.CenterX - Contact.CenterX);
        var dy = (int)Math.Round(input.Geometry.Contact.CenterY - Contact.CenterY);
        return Compare(Feature, input.Contact, input.Feature, dx, dy, policy, token)
            .Bind(c =>
                Compare(FilledObserved, input.Observed, input.FilledObserved, dx, dy, policy, token)
                    .Map(w =>
                        (
                            new Fs50WholeContactSpatialComparison(c, w),
                            Compatible(input.Geometry.Contact, Contact, policy)
                                && Compatible(input.ObservedGeometry.Contact, Observed, policy)
                        )
                    )
            );
    }

    private static Result<Fs50WholeContactSpatialResult, CaptureError> Compare(
        CaptureRegionGeometry anchoredFeature,
        ReadOnlySpan<byte> current,
        ReadOnlySpan<byte> feature,
        int dx,
        int dy,
        Fs50WholeContactPolicy policy,
        CancellationToken token
    )
    {
        var aligned = anchoredFeature.Translate(dx, dy);
        if (aligned.IsFailure)
            return aligned.Error;
        using var copy = new Fs50TemporaryPlane();
        using var corroborated = new Fs50TemporaryPlane();
        using var empty = new Fs50TemporaryPlane();
        aligned.Value.AsSpan().CopyTo(copy.Buffer);
        _ = Fs50MaskMorphology.ExpandInto(
            copy.Buffer,
            policy.Evidence.Options.SupportedExpansion,
            corroborated.Buffer
        );
        return Fs50WholeContactExterior.Measure(
            current,
            feature,
            corroborated.PixelsSpan,
            empty.PixelsSpan,
            policy.Options.MinimumUncorroboratedArea,
            token
        );
    }

    private static bool Compatible(
        CaptureRegionGeometry current,
        CaptureRegionGeometry original,
        Fs50WholeContactPolicy policy
    ) =>
        current.DisplacementFrom(original).Value
            <= original.Diagonal * policy.Presentation.MajorDisplacementFraction
        && current.IntersectionOverUnion(original).Value >= policy.Presentation.MinimumOverlap;
}
