// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

using NUnit.Framework;

internal static class ContactEvidenceFixtures
{
    internal static byte[] Speckles(
        int radiusX,
        int radiusY,
        int low,
        int high,
        uint seed,
        int stretch = 1
    )
    {
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        for (var y = 375 - radiusY; y <= 375 + radiusY; y += stretch)
        for (var x = 400 - radiusX; x <= 400 + radiusX; x++)
        {
            if (Math.Pow((x - 400.0) / radiusX, 2) + Math.Pow((y - 375.0) / radiusY, 2) > 1)
                continue;
            seed = unchecked((seed * 1664525) + 1013904223);
            var value = (byte)(low + ((seed >> 16) % (high - low + 1)));
            for (var row = y; row < Math.Min(y + stretch, 375 + radiusY + 1); row++)
                pixels[(row * 800) + x] = value;
        }
        return pixels;
    }

    internal static byte[] PublicFinger(string name, int width = 512, int height = 512, int low = 0)
    {
        var source = File.ReadAllBytes(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "sd04", name)
        );
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var value = source[((y + ((512 - height) / 2)) * 512) + x + ((512 - width) / 2)];
            pixels[((y + 119) * 800) + x + 144] = (byte)(low + ((value * (255 - low)) / 255));
        }
        return pixels;
    }

    internal static IEnumerable<string> PublicNames() =>
        Directory
            .GetFiles(
                Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "sd04"),
                "*.gray8"
            )
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order();
}
