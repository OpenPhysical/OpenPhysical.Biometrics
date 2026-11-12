// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

internal static class Fs50WholeContactProofFactory
{
    internal static Fs50WholeContactProof Spatial(
        Fs50WholeContactInput input,
        Fs50WholeContactPolicy policy
    ) =>
        Create(
            input,
            policy,
            input.CaptureReady ? Fs50WholeContactDecision.SpatialCapture
                : input.Region.Issue == Fs50ContactIssue.TooLight
                    ? Fs50WholeContactDecision.SpatialExposure
                : Fs50WholeContactDecision.Unavailable,
            Unknown(input.Contact),
            Unknown(input.Observed),
            default,
            default
        );

    internal static Fs50WholeContactProof Create(
        Fs50WholeContactInput input,
        Fs50WholeContactPolicy policy,
        Fs50WholeContactDecision decision,
        Fs50WholeContactMetrics metrics,
        Fs50WholeContactMetrics observedMetrics,
        Maybe<Fs50EmptySensorReference> reference,
        Maybe<string> anchor
    )
    {
        var identity = new Fs50WholeContactIdentity(
            input.Region.Diagnostics.SourceDigest,
            input.Frame.Sequence,
            input.Frame.CapturedAt,
            input.Geometry.ContactDigest,
            input.Geometry.FeatureDigest,
            input.Region.Diagnostics.PolicyDigest,
            Fs50ContactContourPolicy.Version,
            input.Key.SessionId,
            input.Key.Digest,
            input.Key.Exposure
        );
        return new Fs50WholeContactProof(
            new(
                decision,
                identity,
                policy,
                reference.Map(value => value.Digest),
                reference.Map(value => value.Key.Digest),
                anchor,
                metrics,
                Ridge(input),
                observedMetrics
            )
        );
    }

    private static Fs50WholeContactRidgeEvidence Ridge(Fs50WholeContactInput input) =>
        new(
            Fs50PolicyDigest.Pixels(input.Region.Diagnostics.ObservedContactMask.AsSpan()),
            input.Region.SpatialStatistics.Match(value => value.Area, () => 0),
            input.Region.Diagnostics.RidgeExteriorArea,
            input.Region.Diagnostics.LargestRidgeExteriorArea
        );

    internal static Fs50WholeContactDecision ForCapture(
        Fs50WholeContactDecision decision,
        Fs50WholeContactInput input
    ) =>
        input.CaptureReady
            ? decision
            : decision switch
            {
                Fs50WholeContactDecision.QualifiedReference =>
                    Fs50WholeContactDecision.ExposureReference,
                Fs50WholeContactDecision.QualifiedGeometry =>
                    Fs50WholeContactDecision.ExposureGeometry,
                _ => decision,
            };

    internal static Fs50WholeContactDecision Decision(
        Fs50WholeContactSpatialComparison result,
        Fs50WholeContactPolicy policy,
        bool compatibleGeometry = true
    )
    {
        var contact = Decision(result.Contact, policy, compatibleGeometry);
        var observed = Decision(result.Observed, policy, compatibleGeometry);
        if (
            contact == Fs50WholeContactDecision.StaticExterior
            || observed == Fs50WholeContactDecision.StaticExterior
        )
            return Fs50WholeContactDecision.StaticExterior;
        if (
            contact == Fs50WholeContactDecision.UnresolvedExterior
            || observed == Fs50WholeContactDecision.UnresolvedExterior
        )
            return Fs50WholeContactDecision.UnresolvedExterior;
        return contact;
    }

    internal static Fs50WholeContactDecision Decision(
        Fs50WholeContactSpatialResult result,
        Fs50WholeContactPolicy policy,
        bool compatibleGeometry = true
    ) =>
        result.Metrics.LargestUncorroboratedExteriorArea >= policy.Options.MinimumUncorroboratedArea
            ? result.SubstantialStatic
                ? Fs50WholeContactDecision.StaticExterior
                : Fs50WholeContactDecision.UnresolvedExterior
            : compatibleGeometry
                ? Fs50WholeContactDecision.QualifiedGeometry
                : Fs50WholeContactDecision.Unavailable;

    internal static Fs50WholeContactMetrics Unknown(ReadOnlySpan<byte> contact)
    {
        var area = 0;
        foreach (var value in contact)
            area += value;
        return new(area, 0, area, 0, area, area);
    }
}
