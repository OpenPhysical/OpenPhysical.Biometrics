// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Formats.Fingerprints;

internal static class AlignmentTestData
{
    internal static ImmutableArray<FingerprintMinutia> Points(int seed = 17)
    {
        var random = new Random(seed);
        return Enumerable
            .Range(0, 48)
            .Select(_ => new FingerprintMinutia(
                (byte)random.Next(1, 3),
                (ushort)random.Next(180, 520),
                (ushort)random.Next(170, 550),
                (byte)random.Next(180),
                80
            ))
            .ToImmutableArray();
    }

    internal static FingerprintRecord Record(ImmutableArray<FingerprintMinutia> points) =>
        new(1, 1, 0, 2400, 2400, 197, 197, [new(2, 0, 0, 80, points, [])]);

    internal static FingerprintRecord Transform(
        FingerprintRecord source,
        double degrees,
        double scale,
        double tx,
        double ty,
        ushort xResolution,
        ushort yResolution
    )
    {
        var radians = degrees * Math.PI / 180;
        var a = scale * Math.Cos(radians);
        var b = scale * Math.Sin(radians);
        var points = source
            .Views[0]
            .Minutiae.Select(point =>
            {
                var x = a * point.X - b * point.Y + tx;
                var y = b * point.X + a * point.Y + ty;
                var angle = (point.Angle * 2 - degrees + 720) % 360 * Math.PI / 180;
                var projectedAngle = ProjectAngle(angle, xResolution, yResolution);
                return point with
                {
                    X = (ushort)Math.Round(x * xResolution / 197),
                    Y = (ushort)Math.Round(y * yResolution / 197),
                    Angle = (byte)((int)Math.Round(projectedAngle / 2) % 180),
                };
            })
            .ToImmutableArray();
        return source with
        {
            XResolution = xResolution,
            YResolution = yResolution,
            Views = [source.Views[0] with { Minutiae = points }],
        };
    }

    private static double ProjectAngle(double angle, ushort xResolution, ushort yResolution) =>
        (
            Math.Atan2(Math.Sin(angle) * yResolution, Math.Cos(angle) * xResolution) * 180 / Math.PI
            + 360
        ) % 360;
}
