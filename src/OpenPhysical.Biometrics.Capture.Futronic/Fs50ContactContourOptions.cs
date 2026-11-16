// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Application context and artificial-cut choices, without changing contact feature admission.</summary>
public sealed record Fs50ContactContourOptions
{
    /// <summary>Gets the Chebyshev context margin around the full-contact convex envelope.</summary>
    public int ContextMargin { get; init; } = 32;

    /// <summary>Gets the feature exclusion distance from pixels actually modified outside context.</summary>
    public int ModifiedBoundaryGuard { get; init; } = 8;
}
