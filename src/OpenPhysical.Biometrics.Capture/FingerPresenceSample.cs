// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using System.Collections.Immutable;

internal sealed record FingerPresenceSample(
    CaptureFrame Frame,
    ImmutableArray<byte> Mask,
    FingerFrameGeometry Geometry,
    long At
)
{
    internal static FingerPresenceSample Create(
        CaptureFrame frame,
        ImmutableArray<byte> mask,
        long at
    ) => new(frame, mask, FingerFrameGeometry.Create(frame, mask), at);

    internal bool Compatible(CaptureFrame next) =>
        Frame.Width == next.Width
        && Frame.Height == next.Height
        && Frame.Provenance.Device.DeviceId == next.Provenance.Device.DeviceId
        && Frame.Provenance.DriverVersion == next.Provenance.DriverVersion
        && Frame.Provenance.Settings.Corrected == next.Provenance.Settings.Corrected
        && Frame.Provenance.Settings.Rolled == next.Provenance.Settings.Rolled
        && Frame.Provenance.CorrectionVersion == next.Provenance.CorrectionVersion
        && Frame
            .Provenance.CalibrationDigest.AsSpan()
            .SequenceEqual(next.Provenance.CalibrationDigest.AsSpan())
        && Frame.Provenance.Resolution.Kind == next.Provenance.Resolution.Kind
        && Frame.Provenance.Resolution.HorizontalPpi == next.Provenance.Resolution.HorizontalPpi
        && Frame.Provenance.Resolution.VerticalPpi == next.Provenance.Resolution.VerticalPpi;

    internal bool MajorFrom(FingerPresenceSample first, FingerCapturePolicy policy)
    {
        var displacement = Geometry.Region.DisplacementFrom(first.Geometry.Region).Value;
        if (displacement > first.Geometry.Diagonal * policy.MajorDisplacementFraction)
            return true;
        return Geometry.Region.IntersectionOverUnion(first.Geometry.Region).Value
            < policy.MinimumOverlap;
    }
}
