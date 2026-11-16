// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

internal static class Fs50ContactComponents
{
    internal static bool Substantial(int[] positions, int count, Fs50EvidenceOptions options)
    {
        if (count < options.MinimumArea)
            return false;
        int minX = 800,
            maxX = 0,
            minY = 750,
            maxY = 0;
        for (var i = 0; i < count; i++)
        {
            minX = Math.Min(minX, positions[i] % 800);
            maxX = Math.Max(maxX, positions[i] % 800);
            minY = Math.Min(minY, positions[i] / 800);
            maxY = Math.Max(maxY, positions[i] / 800);
        }
        return maxX - minX + 1 >= options.MinimumWidth && maxY - minY + 1 >= options.MinimumHeight;
    }

    internal static int Flood(byte[] mask, int[] queue, int start)
    {
        int head = 0,
            tail = 1;
        queue[0] = start;
        mask[start] = 0;
        while (head < tail)
        {
            var index = queue[head++];
            int x = index % 800,
                y = index / 800;
            for (var yy = Math.Max(0, y - 1); yy <= Math.Min(749, y + 1); yy++)
            for (var xx = Math.Max(0, x - 1); xx <= Math.Min(799, x + 1); xx++)
            {
                var next = yy * 800 + xx;
                if (mask[next] == 0)
                    continue;
                mask[next] = 0;
                queue[tail++] = next;
            }
        }
        return tail;
    }
}
