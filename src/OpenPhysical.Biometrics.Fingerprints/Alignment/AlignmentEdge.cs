// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

internal sealed record AlignmentEdge(AlignmentPoint First, AlignmentPoint Second)
{
    internal double Length =>
        Math.Sqrt(
            (Second.X - First.X) * (Second.X - First.X)
                + (Second.Y - First.Y) * (Second.Y - First.Y)
        );
    internal double Direction => Math.Atan2(Second.Y - First.Y, Second.X - First.X);
}
