// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

// Explicit support planes exercise the public geometry contract, not fabricated detector evidence.
internal sealed class SupportGeometryFixture
{
    internal byte[] Candidate { get; } = new byte[800 * 750];
    internal byte[] Support { get; } = new byte[800 * 750];
    internal byte[] Retained { get; } = new byte[800 * 750];

    internal static bool Rectangle(byte[] mask, int x, int y, int width, int height, byte value = 1)
    {
        for (var yy = y; yy < y + height; yy++)
        for (var xx = x; xx < x + width; xx++)
            mask[yy * 800 + xx] = value;
        return true;
    }

    internal static SupportGeometryFixture Solid()
    {
        var example = new SupportGeometryFixture();
        foreach (var plane in new[] { example.Candidate, example.Support, example.Retained })
            _ = Rectangle(plane, 300, 250, 160, 240);
        return example;
    }

    internal bool WeakRectangle(int x, int y, int width, int height)
    {
        _ = Rectangle(Support, x, y, width, height, 0);
        return Rectangle(Retained, x, y, width, height, 0);
    }
}
