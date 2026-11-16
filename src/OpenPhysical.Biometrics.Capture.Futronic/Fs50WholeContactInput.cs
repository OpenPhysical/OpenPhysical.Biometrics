// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

internal sealed class Fs50WholeContactInput(
    CaptureFrame frame,
    Fs50ContactRegion region,
    Fs50EmptySensorKey key,
    Fs50WholeContactGeometry geometry,
    Fs50WholeContactGeometry observedGeometry
)
{
    internal CaptureFrame Frame { get; } = frame;
    internal Fs50ContactRegion Region { get; } = region;
    internal Fs50EmptySensorKey Key { get; } = key;
    internal Fs50WholeContactGeometry Geometry { get; } = geometry;
    internal Fs50WholeContactGeometry ObservedGeometry { get; } = observedGeometry;
    internal ReadOnlySpan<byte> Contact => Geometry.Contact.Mask.AsSpan();
    internal ReadOnlySpan<byte> Feature => Geometry.Feature.Mask.AsSpan();
    internal ReadOnlySpan<byte> Observed => ObservedGeometry.Contact.Mask.AsSpan();
    internal ReadOnlySpan<byte> FilledObserved => ObservedGeometry.Feature.Mask.AsSpan();
    internal bool SingleContact =>
        Region.SpatialComponentCount == 1 && Region.SpatialStatistics.HasValue;
    internal bool CaptureReady =>
        Region.Issue == Fs50ContactIssue.None
        && Region.ComponentCount == 1
        && SingleContact
        && Region.SpatialStatistics.Value.Signal >= Region.Diagnostics.Policy.Options.MinimumSignal;

    internal static Result<Fs50WholeContactInput, CaptureError> Create(
        CaptureFrame frame,
        Fs50ContactRegion region,
        Fs50WholeContactPolicy policy,
        string sessionId,
        Fs50WholeContactGeometryPairCache geometry,
        CancellationToken token
    )
    {
        if (region is null)
            return new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Original contact evidence is required."
            );
        return Fs50FrozenEvidence
            .Validate(
                frame,
                region.Diagnostics,
                policy,
                Fs50ContactContourErrorCodes.SourceMismatch,
                token
            )
            .Ensure(
                () => region.Diagnostics.PolicyDigest == policy.Evidence.Digest,
                new CaptureError(
                    CaptureErrorCodes.InvalidFrame,
                    "Original evidence and qualification policy differ."
                )
            )
            .Bind(() => Fs50EmptySensorKey.Create(frame, sessionId))
            .Bind(key => ForGeometry(frame, region, key, geometry, token));
    }

    private static Result<Fs50WholeContactInput, CaptureError> ForGeometry(
        CaptureFrame frame,
        Fs50ContactRegion region,
        Fs50EmptySensorKey key,
        Fs50WholeContactGeometryPairCache geometry,
        CancellationToken token
    ) =>
        geometry
            .Get(
                key,
                frame.Provenance.OutputGeometry,
                region.ContactMask,
                region.Diagnostics.ObservedContactMask,
                token
            )
            .Map(shapes => new Fs50WholeContactInput(
                frame,
                region,
                key,
                shapes.Contact,
                shapes.Observed
            ));
}
