// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Buffers;

internal sealed class Fs50HysteresisSeeds : IDisposable
{
    private const int Pixels = 800 * 750;
    private readonly Fs50TemporaryPlane remaining = new();
    private readonly Fs50TemporaryPlane component = new();
    private readonly Fs50TemporaryPlane support = new();
    private readonly int[] queue = ArrayPool<int>.Shared.Rent(Pixels);

    internal int Write(
        ReadOnlySpan<byte> pixels,
        ReadOnlySpan<byte> strong,
        ReadOnlySpan<int> positions,
        Fs50EvidenceOptions options,
        byte[] output
    )
    {
        foreach (var index in positions)
            remaining.Buffer[index] = strong[index];
        var seeds = 0;
        foreach (var index in positions)
        {
            if (remaining.Buffer[index] == 0)
                continue;
            var count = Fs50ContactComponents.Flood(remaining.Buffer, queue, index);
            if (WriteSeed(pixels, count, options, output))
                seeds++;
        }
        return seeds;
    }

    private bool WriteSeed(
        ReadOnlySpan<byte> pixels,
        int count,
        Fs50EvidenceOptions options,
        byte[] output
    )
    {
        if (!Fs50ContactComponents.Substantial(queue, count, options))
            return false;
        support.PixelsSpan.Clear();
        for (var i = 0; i < count; i++)
            component.Buffer[queue[i]] = 1;
        var valid = Fs50RidgeEvidence.Write(
            pixels,
            component.Buffer,
            queue,
            count,
            options,
            support.Buffer
        );
        for (var i = 0; i < count; i++)
        {
            if (valid)
                output[queue[i]] = 1;
            component.Buffer[queue[i]] = 0;
        }
        return valid;
    }

    public void Dispose()
    {
        remaining.Dispose();
        component.Dispose();
        support.Dispose();
        ArrayPool<int>.Shared.Return(queue, clearArray: true);
    }
}
