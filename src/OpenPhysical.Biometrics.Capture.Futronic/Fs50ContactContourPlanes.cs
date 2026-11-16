// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;

/// <summary>Independent binary geometry planes; factories validate their relationships.</summary>
/// <param name="ContactMask">Selected connected measured texture C.</param>
/// <param name="FilledHoleMask">Enclosed inferred holes H, never called measured support.</param>
/// <param name="FeatureMask">Contact and enclosed holes F.</param>
/// <param name="SupportMask">Original measured ridge support intersected with C.</param>
/// <param name="ContextMask">Convex full-contact envelope and context margin E.</param>
/// <param name="UncertainMask">Feature area without selected measured ridge support F minus S.</param>
public sealed record Fs50ContactContourPlanes(
    ImmutableArray<byte> ContactMask,
    ImmutableArray<byte> FilledHoleMask,
    ImmutableArray<byte> FeatureMask,
    ImmutableArray<byte> SupportMask,
    ImmutableArray<byte> ContextMask,
    ImmutableArray<byte> UncertainMask
);
