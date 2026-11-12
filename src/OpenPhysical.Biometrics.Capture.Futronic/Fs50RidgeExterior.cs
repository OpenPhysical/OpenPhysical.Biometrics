// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Buffers;

internal static class Fs50RidgeExterior
{
    internal static (int Area, int Largest) Measure(byte[] contact, ReadOnlySpan<byte> reach)
    {
        var filled = Fs50ContourTopology.Fill(reach, CancellationToken.None).Value;
        var queue = ArrayPool<int>.Shared.Rent(reach.Length);
        try
        {
            var area = 0;
            for (var i = 0; i < reach.Length; i++)
            {
                filled[i] = (byte)(contact[i] != 0 && filled[i] == 0 ? 1 : 0);
                area += filled[i];
            }
            var largest = 0;
            for (var i = 0; i < reach.Length; i++)
                if (filled[i] != 0)
                    largest = Math.Max(largest, Fs50ContactComponents.Flood(filled, queue, i));
            return (area, largest);
        }
        finally
        {
            Array.Clear(filled);
            ArrayPool<int>.Shared.Return(queue, clearArray: true);
        }
    }
}
