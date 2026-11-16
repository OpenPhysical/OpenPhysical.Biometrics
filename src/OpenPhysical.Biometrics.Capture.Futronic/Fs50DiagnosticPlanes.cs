// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;

internal sealed record Fs50DiagnosticPlanes(
    ImmutableArray<byte> Candidate,
    ImmutableArray<byte> RidgeSupport,
    ImmutableArray<byte> Accepted,
    ImmutableArray<byte> Rejected,
    ImmutableArray<byte> Inner
)
{
    private static readonly ImmutableArray<byte> Zero = ImmutableArray.Create(new byte[800 * 750]);
    internal static Fs50DiagnosticPlanes Empty { get; } = new(Zero, Zero, Zero, Zero, Zero);
}
