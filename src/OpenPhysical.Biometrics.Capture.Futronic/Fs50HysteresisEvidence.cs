// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Buffers;

internal static class Fs50HysteresisEvidence
{
    private const int Pixels = 800 * 750;

    internal static Fs50HysteresisGrowth Grow(
        ReadOnlySpan<byte> pixels,
        byte[] weakComponent,
        ReadOnlySpan<byte> strongTexture,
        ReadOnlySpan<int> positions,
        byte[] localSupport,
        Fs50EvidenceOptions options,
        byte[] chosenContact,
        byte[] retainedSupport
    )
    {
        using var primary = new Fs50TemporaryPlane();
        using var reach = new Fs50TemporaryPlane();
        using var classifier = new Fs50HysteresisSeeds();
        var seeds = classifier.Write(pixels, strongTexture, positions, options, primary.Buffer);
        _ = Fs50MaskMorphology.ExpandInto(localSupport, options.SupportedExpansion, reach.Buffer);
        Array.Clear(chosenContact, 0, Pixels);
        var area =
            seeds == 0
                ? CopyWeak(weakComponent, chosenContact)
                : WriteConnected(weakComponent, primary.Buffer, reach.Buffer, chosenContact);
        var exterior = Rejection(weakComponent, localSupport, reach, options);
        var retained = WriteRetained(chosenContact, reach.Buffer, retainedSupport);
        return new(seeds, area, retained, exterior.Area, exterior.Largest);
    }

    private static (int Area, int Largest) Rejection(
        byte[] weak,
        byte[] support,
        Fs50TemporaryPlane growthReach,
        Fs50EvidenceOptions options
    )
    {
        if (options.RidgeBoundaryTolerance == options.SupportedExpansion)
            return Fs50RidgeExterior.Measure(weak, growthReach.PixelsSpan);
        using var rejectionReach = new Fs50TemporaryPlane();
        _ = Fs50MaskMorphology.ExpandInto(
            support,
            options.RidgeBoundaryTolerance,
            rejectionReach.Buffer
        );
        return Fs50RidgeExterior.Measure(weak, rejectionReach.PixelsSpan);
    }

    private static int WriteRetained(byte[] contact, byte[] reach, byte[] output)
    {
        var area = 0;
        for (var i = 0; i < Pixels; i++)
        {
            output[i] = (byte)(contact[i] != 0 && reach[i] != 0 ? 1 : 0);
            area += output[i];
        }
        return area;
    }

    private static int WriteConnected(byte[] weak, byte[] primary, byte[] reach, byte[] output)
    {
        using var allowed = new Fs50TemporaryPlane();
        for (var i = 0; i < Pixels; i++)
            allowed.Buffer[i] = (byte)(primary[i] != 0 || weak[i] != 0 && reach[i] != 0 ? 1 : 0);
        var queue = ArrayPool<int>.Shared.Rent(Pixels);
        try
        {
            var area = 0;
            for (var i = 0; i < Pixels; i++)
            {
                if (allowed.Buffer[i] == 0)
                    continue;
                var count = Fs50ContactComponents.Flood(allowed.Buffer, queue, i);
                if (!ContainsSeed(queue.AsSpan(0, count), primary))
                    continue;
                for (var j = 0; j < count; j++)
                    output[queue[j]] = 1;
                area += count;
            }
            return area;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(queue, clearArray: true);
        }
    }

    private static bool ContainsSeed(ReadOnlySpan<int> positions, byte[] primary)
    {
        foreach (var index in positions)
            if (primary[index] != 0)
                return true;
        return false;
    }

    private static int CopyWeak(byte[] weak, byte[] output)
    {
        Array.Copy(weak, output, Pixels);
        var area = 0;
        for (var i = 0; i < Pixels; i++)
            area += weak[i];
        return area;
    }
}
