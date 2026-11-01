// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial.Tests;

using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

internal static class TestFixtures
{
    internal static string PathOf(string name) =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", name);

    internal static byte[] Bytes(string name) => File.ReadAllBytes(PathOf(name));

    internal static PortraitPreparer NewPreparer() => new(NullLogger<PortraitPreparer>.Instance);

    internal static FacialRecordPreparer NewRecordPreparer() =>
        new(NullLogger<FacialRecordPreparer>.Instance);

    // A synthetic mesh with only the anatomical landmarks the crop policy reads.
    internal static (PortraitFace Face, ReviewedPortraitGeometry Review) Geometry()
    {
        var points = Enumerable.Repeat(new FaceMeshPoint(600, 700, 0), 478).ToArray();
        points[33] = new(510, 650, 0);
        points[133] = new(550, 650, 0);
        points[362] = new(650, 650, 0);
        points[263] = new(690, 650, 0);
        points[152] = new(600, 950, 0);
        points[168] = new(600, 670, 0);
        points[13] = new(600, 800, 0);
        points[14] = new(600, 810, 0);
        return (
            new(0, .99, .99, points.ToImmutableArray(), new(440, 650), new(760, 650)),
            new(
                1,
                "fixture",
                1200,
                1600,
                0,
                new PortraitPoint(600, 400),
                default,
                new PortraitPoint(440, 650),
                new PortraitPoint(760, 650)
            )
        );
    }
}
