// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>An explicit set of required capture modalities for one person.</summary>
/// <param name="Id">Stable plan identity.</param>
/// <param name="Label">Operator-facing plan label.</param>
public sealed record CapturePlanChoice(string Id, string Label)
{
    /// <summary>Gets the default face and two-finger workflow.</summary>
    public static CapturePlanChoice Full { get; } = new("full", "Face and fingerprints");

    /// <summary>Gets the facial-only workflow.</summary>
    public static CapturePlanChoice Face { get; } = new("face", "Face only");

    /// <summary>Gets the fingerprint-only workflow.</summary>
    public static CapturePlanChoice Fingerprints { get; } =
        new("fingerprints", "Fingerprints only");

    /// <summary>Gets all supported explicit plans.</summary>
    public static ImmutableArray<CapturePlanChoice> All => [Full, Face, Fingerprints];

    internal bool Includes(string section) => Id == "full" || Id == section;

    /// <inheritdoc/>
    public override string ToString() => Label;
}
