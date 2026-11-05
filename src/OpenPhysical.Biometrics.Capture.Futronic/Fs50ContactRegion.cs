// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;

/// <summary>Observed textured region; this is neither liveness nor identity evidence.</summary>
public sealed class Fs50ContactRegion
{
    internal Fs50ContactRegion(byte[] mask, int area, double contrast, double signal)
    {
        Mask = ImmutableArray.CreateRange(mask);
        Area = area;
        Contrast = contrast;
        Signal = signal;
    }

    /// <summary>Gets the tightly packed binary mask for the corrected image.</summary>
    public ImmutableArray<byte> Mask { get; }

    /// <summary>Gets the selected component's area.</summary>
    public int Area { get; }

    /// <summary>Gets whether substantial textured contact was observed.</summary>
    public bool Present => Area > 0;

    /// <summary>Gets the 95th minus fifth percentile grayscale contrast.</summary>
    public double Contrast { get; }

    /// <summary>Gets the 95th percentile inverted intensity for exposure control.</summary>
    public double Signal { get; }
}
