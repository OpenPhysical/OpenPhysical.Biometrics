// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

internal static class WeakContactFixture
{
    internal static bool Paint(byte[] pixels, int left, int top, int width, int height)
    {
        for (var y = top; y < top + height; y++)
        for (var x = left; x < left + width; x++)
            pixels[y * 800 + x] = (x * 17 + y * 31) % 11 < 4 ? (byte)230 : (byte)250;
        return true;
    }
}
