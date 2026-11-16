// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using System.Collections.Immutable;

internal sealed record PortraitPoseReference(
    int Width,
    int Height,
    double Pitch,
    double Yaw,
    double Roll,
    ImmutableArray<ImmutableArray<double>> Points
)
{
    internal ImmutableArray<FaceMeshPoint> Landmarks =>
        Points.Select(p => new FaceMeshPoint(p[0], p[1], p[2])).ToImmutableArray();
}
