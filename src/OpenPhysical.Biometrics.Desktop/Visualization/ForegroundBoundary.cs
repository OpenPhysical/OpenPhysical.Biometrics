// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Capture.Futronic;

namespace OpenPhysical.Biometrics.Desktop;

internal static class ForegroundBoundary
{
    internal static ImmutableArray<ForegroundEdge> Create(
        Fs50ContactRegion contact,
        int width,
        int height
    ) => Create(contact.Mask, width, height);

    internal static ImmutableArray<ForegroundEdge> Create(
        ImmutableArray<byte> mask,
        int width,
        int height
    )
    {
        var edges = ImmutableArray.CreateBuilder<ForegroundEdge>();
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var index = y * width + x;
            if (mask[index] == 0)
                continue;
            if (y == 0 || mask[index - width] == 0)
                edges.Add(new ForegroundEdge(x, y, x + 1, y));
            if (y == height - 1 || mask[index + width] == 0)
                edges.Add(new ForegroundEdge(x, y + 1, x + 1, y + 1));
            if (x == 0 || mask[index - 1] == 0)
                edges.Add(new ForegroundEdge(x, y, x, y + 1));
            if (x == width - 1 || mask[index + 1] == 0)
                edges.Add(new ForegroundEdge(x + 1, y, x + 1, y + 1));
        }
        return edges.ToImmutable();
    }
}
