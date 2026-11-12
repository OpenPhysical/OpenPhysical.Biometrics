// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

internal static class HysteresisFingerFixture
{
    internal static byte[] Annulus()
    {
        var pixels = Fs50EvidenceFrameFixture.Empty();
        for (var y = 150; y <= 550; y++)
        for (var x = 250; x <= 550; x++)
        {
            var outer = Math.Pow((x - 400) / 150.0, 2) + Math.Pow((y - 350) / 200.0, 2);
            if (outer > 1)
                continue;
            var core = Math.Pow((x - 400) / 70.0, 2) + Math.Pow((y - 350) / 100.0, 2);
            var wave = Math.Sin((x - 400) * Math.PI / 4);
            pixels[y * 800 + x] = (byte)Math.Round(core <= 1 ? 145 + 65 * wave : 242 + 10 * wave);
        }
        return pixels;
    }

    internal static byte[] WeakOnly(int mean)
    {
        var pixels = Fs50EvidenceFrameFixture.Empty();
        for (var y = 150; y <= 550; y++)
        for (var x = 250; x <= 550; x++)
        {
            if (Math.Pow((x - 400) / 150.0, 2) + Math.Pow((y - 350) / 200.0, 2) > 1)
                continue;
            pixels[y * 800 + x] = (byte)Math.Round(mean + 10 * Math.Sin((x - 400) * Math.PI / 4));
        }
        return pixels;
    }

    internal static byte[] StrongSmear()
    {
        var pixels = ContactEvidenceFixtures.Speckles(140, 210, 90, 240, 27);
        _ = WholeContactRig.Ridges(pixels, 368, 330, 64, 96);
        return pixels;
    }

    internal static byte[] WeakDots()
    {
        var pixels = Fs50EvidenceFrameFixture.Empty();
        for (var y = 165; y <= 585; y++)
        for (var x = 260; x <= 540; x++)
        {
            if (Math.Pow((x - 400) / 140.0, 2) + Math.Pow((y - 375) / 210.0, 2) > 1)
                continue;
            var dot = (x % 12 - 6) * (x % 12 - 6) + (y % 12 - 6) * (y % 12 - 6) <= 4;
            pixels[y * 800 + x] = dot ? (byte)230 : (byte)250;
        }
        _ = WholeContactRig.Ridges(pixels, 368, 330, 64, 96);
        return pixels;
    }

    internal static byte[] AttachedStrongSmear(bool farSupport)
    {
        var pixels = Fs50EvidenceFrameFixture.Empty();
        uint seed = 27;
        for (var y = 200; y < 500; y++)
        for (var x = 230; x < 570; x++)
        {
            if (
                farSupport
                && (
                    y < 270
                    || y >= 430
                    || x > 326 && Math.Abs(y - 350) > 80 - (x - 326) * 56.0 / 244
                )
            )
                continue;
            seed = unchecked(seed * 1664525 + 1013904223);
            pixels[y * 800 + x] = (byte)(90 + (seed >> 16) % 151);
        }
        _ = WholeContactRig.Ridges(
            pixels,
            230,
            farSupport ? 270 : 200,
            farSupport ? 96 : 220,
            farSupport ? 160 : 300
        );
        if (farSupport)
            _ = WholeContactRig.Ridges(pixels, 522, 326, 48, 48);
        return pixels;
    }

    internal static byte[] WeakBridge()
    {
        var pixels = Fs50EvidenceFrameFixture.Empty();
        for (var y = 310; y < 390; y++)
        for (var x = 300; x < 500; x++)
            pixels[y * 800 + x] = (byte)Math.Round(242 + 10 * Math.Sin(x * Math.PI / 4));
        _ = WholeContactRig.Ridges(pixels, 230, 250, 100, 200);
        _ = WholeContactRig.Ridges(pixels, 470, 250, 100, 200);
        return pixels;
    }
}
