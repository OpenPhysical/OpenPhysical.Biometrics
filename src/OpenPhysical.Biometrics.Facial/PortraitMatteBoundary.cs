// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

internal static class PortraitMatteBoundary
{
    internal static byte[] Refine(
        float[] confidence,
        byte[] face,
        int width,
        int height,
        PortraitBackgroundPolicy policy
    )
    {
        var foreground = confidence.Select(value => Foreground(value, policy)).ToArray();
        var contracted = PortraitAlphaRefinement.Contract(
            foreground,
            width,
            height,
            policy.DefringeRadiusPixels
        );
        var smooth = PortraitAlphaRefinement.Smooth(
            contracted,
            width,
            height,
            policy.FeatherPixels
        );
        try
        {
            return Replacement(smooth, face, width, height, policy.ProtectionRadiusPixels);
        }
        finally
        {
            Array.Clear(foreground);
            Array.Clear(contracted);
            Array.Clear(smooth);
        }
    }

    private static byte[] Replacement(float[] smooth, byte[] face, int width, int height, int guard)
    {
        var result = new byte[smooth.Length];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            result[y * width + x] = Protected(face, width, height, x, y, guard)
                ? (byte)0
                : (byte)Math.Clamp(Math.Round((1 - smooth[y * width + x]) * 255), 0, 255);
        return result;
    }

    private static float Foreground(float confidence, PortraitBackgroundPolicy policy)
    {
        var t = Math.Clamp(
            (confidence - policy.MaximumBackgroundPersonConfidence)
                / policy.ConfidenceTransitionWidth
                + .5,
            0,
            1
        );
        return (float)(t * t * (3 - 2 * t));
    }

    private static bool Protected(byte[] face, int width, int height, int x, int y, int radius)
    {
        for (var dy = -radius; dy <= radius; dy++)
        for (var dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy > radius * radius)
                continue;
            var sx = x + dx;
            var sy = y + dy;
            if (sx >= 0 && sx < width && sy >= 0 && sy < height && face[sy * width + sx] != 0)
                return true;
        }
        return false;
    }
}
