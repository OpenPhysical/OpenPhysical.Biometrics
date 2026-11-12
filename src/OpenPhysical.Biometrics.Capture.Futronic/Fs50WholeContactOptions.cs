// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Spatial whole-attempt admission choices, not anatomy or liveness requirements.</summary>
public sealed record Fs50WholeContactOptions
{
    /// <summary>Gets the side length of clipped C-only photometric windows.</summary>
    public int WindowSize { get; init; } = 32;

    /// <summary>Gets the globally anchored window-center spacing.</summary>
    public int GridStep { get; init; } = 16;

    /// <summary>Gets the connected exterior uncertainty area requiring rejection, without finger aspect constraints.</summary>
    public int MinimumUncorroboratedArea { get; init; } = 4000;
}
