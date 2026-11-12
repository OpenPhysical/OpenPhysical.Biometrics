// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using System.Collections.Immutable;

internal sealed record FingerFrameGeometry(CaptureRegionGeometry Region, double Clipped)
{
    internal double X => Region.CenterX;
    internal double Y => Region.CenterY;
    internal double Diagonal => Region.Diagonal;
    internal int Area => Region.Area;

    internal static FingerFrameGeometry Create(CaptureFrame frame, ImmutableArray<byte> mask)
    {
        var region = CaptureRegionGeometry.Create(frame.Provenance.OutputGeometry, mask).Value;
        var clipped = 0;
        for (var index = 0; index < mask.Length; index++)
        {
            if (mask[index] == 0)
                continue;
            clipped += frame.Pixels[index] is 0 or 255 ? 1 : 0;
        }
        return new(region, region.Area == 0 ? 1 : (double)clipped / region.Area);
    }
}
