// SPDX-License-Identifier: GPL-2.0-or-later
// Adapted from FaceOFFx backdrop evidence; see Notices/FaceOFFx-MIT.txt.
namespace OpenPhysical.Biometrics.Facial;

internal static class PortraitBackdropTexture
{
    internal static bool Eligible(
        ReadOnlySpan<byte> rgb,
        int width,
        int height,
        int index,
        PortraitBackdropOptions options
    )
    {
        var x = index % width;
        var y = index / width;
        for (var c = 0; c < 3; c++)
        {
            double sum = 0,
                square = 0,
                gradient = 0;
            var count = 0;
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (x + dx < 0 || x + dx >= width || y + dy < 0 || y + dy >= height)
                    continue;
                var value = rgb[((y + dy) * width + x + dx) * 3 + c];
                count++;
                sum += value;
                square += value * value;
                gradient += Math.Abs(value - rgb[index * 3 + c]);
            }
            if (
                square / count - Math.Pow(sum / count, 2) > options.MaximumLocalVariance
                || gradient / count > options.MaximumNeighborGradient
            )
                return false;
        }
        return true;
    }

    internal static bool Matches(
        ReadOnlySpan<byte> rgb,
        int index,
        byte[] reference,
        double distance
    )
    {
        for (var c = 0; c < 3; c++)
            if (Math.Abs(rgb[index * 3 + c] - reference[c]) > distance)
                return false;
        return true;
    }
}
