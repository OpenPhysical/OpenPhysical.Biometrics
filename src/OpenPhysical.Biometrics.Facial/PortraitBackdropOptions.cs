// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>Application diagnostic policy, independent of mandatory PIV background review.</summary>
public sealed record PortraitBackdropOptions
{
    /// <summary>Gets the maximum average native neighbor difference per channel.</summary>
    public double MaximumNeighborGradient { get; init; } = 8;

    /// <summary>Gets the maximum native 3-by-3 channel population variance.</summary>
    public double MaximumLocalVariance { get; init; } = 64;

    /// <summary>Gets the maximum channel distance from robust border color.</summary>
    public double MaximumColorDistance { get; init; } = 24;

    /// <summary>Gets the native difference used for edge-density statistics.</summary>
    public double EdgeDifferenceThreshold { get; init; } = 16;

    /// <summary>Gets the minimum candidate area in pixels.</summary>
    public int MinimumCandidatePixels { get; init; } = 16;

    /// <summary>Gets the minimum candidate fraction.</summary>
    public double MinimumCandidateFraction { get; init; } = .01;

    /// <summary>Gets default diagnostic parameters.</summary>
    public static PortraitBackdropOptions Default { get; } = new();

    internal bool IsValid =>
        double.IsFinite(MaximumNeighborGradient)
        && MaximumNeighborGradient is >= 0 and <= 255
        && double.IsFinite(MaximumLocalVariance)
        && MaximumLocalVariance is >= 0 and <= 65025
        && double.IsFinite(MaximumColorDistance)
        && MaximumColorDistance is >= 0 and <= 255
        && double.IsFinite(EdgeDifferenceThreshold)
        && EdgeDifferenceThreshold is >= 0 and <= 255
        && MinimumCandidatePixels > 0
        && double.IsFinite(MinimumCandidateFraction)
        && MinimumCandidateFraction is > 0 and <= 1;
}
