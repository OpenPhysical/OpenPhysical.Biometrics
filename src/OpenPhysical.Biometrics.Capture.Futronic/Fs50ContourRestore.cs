// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

internal static class Fs50ContourRestore
{
    internal static Result<Fs50ContactContourArtifact, CaptureError> Create(
        string source,
        string evidenceVersion,
        string evidencePolicy,
        Fs50ContactContourPolicy policy,
        Fs50ContactContourPlanes planes,
        Fs50ContactContourStatistics statistics,
        CancellationToken token
    )
    {
        if (token.IsCancellationRequested)
            return Fs50MaskErrors.Cancelled;
        if (
            !Fs50PolicyDigest.IsDigest(source)
            || !Fs50PolicyDigest.IsDigest(evidencePolicy)
            || string.IsNullOrWhiteSpace(evidenceVersion)
            || evidenceVersion.Length > 256
            || evidenceVersion.Any(char.IsControl)
            || planes is null
            || statistics is null
            || !ValidPlanes(planes)
        )
            return Invalid();
        return Fs50ContactContourGeometry
            .Create(planes.ContactMask.AsSpan(), planes.SupportMask.AsSpan(), policy, token)
            .Ensure(
                shape => Same(shape.Planes, planes) && shape.Statistics == statistics,
                Invalid()
            )
            .Map(shape => new Fs50ContactContourArtifact(
                source,
                evidenceVersion,
                evidencePolicy,
                shape
            ));
    }

    private static bool ValidPlanes(Fs50ContactContourPlanes planes) =>
        new[]
        {
            planes.ContactMask,
            planes.FilledHoleMask,
            planes.FeatureMask,
            planes.SupportMask,
            planes.ContextMask,
            planes.UncertainMask,
        }.All(plane =>
            !plane.IsDefault && plane.Length == 800 * 750 && plane.All(value => value <= 1)
        );

    private static bool Same(Fs50ContactContourPlanes first, Fs50ContactContourPlanes second) =>
        first.ContactMask.AsSpan().SequenceEqual(second.ContactMask.AsSpan())
        && first.FilledHoleMask.AsSpan().SequenceEqual(second.FilledHoleMask.AsSpan())
        && first.FeatureMask.AsSpan().SequenceEqual(second.FeatureMask.AsSpan())
        && first.SupportMask.AsSpan().SequenceEqual(second.SupportMask.AsSpan())
        && first.ContextMask.AsSpan().SequenceEqual(second.ContextMask.AsSpan())
        && first.UncertainMask.AsSpan().SequenceEqual(second.UncertainMask.AsSpan());

    private static CaptureError Invalid() =>
        new(
            CaptureErrorCodes.InvalidFrame,
            "Saved contour identities, planes or statistics are inconsistent."
        );
}
