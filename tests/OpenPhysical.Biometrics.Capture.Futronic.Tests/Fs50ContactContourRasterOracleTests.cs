// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class Fs50ContactContourRasterOracleTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void ConnectedContactContextMatchesAnIndependentPixelCellTriangle(int turns)
    {
        var shape = Triangle(turns, 0);
        // The hull encloses unit pixel cells, so its diagonal is x+y=5 rather than x+y=4.
        shape.ContextMask.Sum(value => value).Should().Be(19);
        for (var y = 0; y < 5; y++)
        for (var x = 0; x < 5; x++)
        {
            var included = turns switch
            {
                0 => x + y <= 5,
                1 => y - x <= 1,
                2 => x + y >= 3,
                _ => x - y <= 1,
            };
            shape.ContextMask[(300 + y) * 800 + 350 + x].Should().Be(included ? (byte)1 : (byte)0);
        }
    }

    [Test]
    public void ConnectedContactContextDoesNotDependOnTheSixteenPixelEvidenceGridPhase()
    {
        for (var offset = 0; offset < 16; offset++)
        {
            var shape = Triangle(0, offset);
            shape.ContextMask.Sum(value => value).Should().Be(19);
            shape.ContextMask[(302 + offset) * 800 + 352 + offset].Should().Be(1);
            shape.ContextMask[(304 + offset) * 800 + 354 + offset].Should().Be(0);
        }
    }

    private static Fs50ContactContourShape Triangle(int turns, int offset)
    {
        var contact = new byte[800 * 750];
        for (var y = 0; y <= 4; y++)
        for (var x = 0; x <= 4; x++)
        {
            if (!(x == 0 || y == 0 || x + y == 4) || x + y > 4)
                continue;
            var point = Rotate(x, y, turns);
            contact[(300 + offset + point.Y) * 800 + 350 + offset + point.X] = 1;
        }
        return Fs50ContactContourGeometry
            .Create(
                contact,
                contact,
                Fs50ContactContourPolicy.Create(new() { ContextMargin = 0 }).Value,
                CancellationToken.None
            )
            .Value;
    }

    private static (int X, int Y) Rotate(int x, int y, int turns) =>
        turns switch
        {
            0 => (x, y),
            1 => (4 - y, x),
            2 => (4 - x, 4 - y),
            _ => (y, 4 - x),
        };
}
