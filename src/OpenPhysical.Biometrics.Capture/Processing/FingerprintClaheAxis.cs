// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

internal readonly record struct FingerprintClaheAxis(int Lower, int Upper, double Weight)
{
    internal static FingerprintClaheAxis[] Create(int size, int tileSize)
    {
        var tiles = (size + tileSize - 1) / tileSize;
        var centers = new double[tiles];
        for (var i = 0; i < tiles; i++)
            centers[i] = ((i * tileSize) + Math.Min(size, (i + 1) * tileSize) - 1) / 2.0;
        var positions = new FingerprintClaheAxis[size];
        var upper = 0;
        for (var position = 0; position < size; position++)
        {
            while (upper < tiles - 1 && position > centers[upper])
                upper++;
            var lower = Math.Max(0, upper - 1);
            var weight =
                upper == lower
                    ? 0
                    : Math.Clamp(
                        (position - centers[lower]) / (centers[upper] - centers[lower]),
                        0,
                        1
                    );
            positions[position] = new FingerprintClaheAxis(lower, upper, weight);
        }
        return positions;
    }
}
