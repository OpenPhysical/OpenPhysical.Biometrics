// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Buffers;
using CSharpFunctionalExtensions;

internal static class Fs50WholeContactExterior
{
    internal static Result<Fs50WholeContactSpatialResult, CaptureError> Measure(
        ReadOnlySpan<byte> contact,
        ReadOnlySpan<byte> feature,
        ReadOnlySpan<byte> corroborated,
        ReadOnlySpan<byte> observedStatic,
        int threshold,
        CancellationToken token
    )
    {
        using var residual = new Fs50TemporaryPlane();
        using var exterior = new Fs50TemporaryPlane();
        var queue = ArrayPool<int>.Shared.Rent(contact.Length);
        try
        {
            var areas = Prepare(contact, corroborated, residual.Buffer);
            var components = ExteriorComponents(
                residual.Buffer,
                feature,
                exterior.Buffer,
                queue,
                token
            );
            if (components.IsFailure)
                return components.Error;
            return Complete(
                exterior.Buffer,
                observedStatic[..contact.Length],
                queue,
                areas.Contact,
                areas.Corroborated,
                components.Value,
                threshold,
                token
            );
        }
        finally
        {
            ArrayPool<int>.Shared.Return(queue, clearArray: true);
        }
    }

    private static (int Contact, int Corroborated) Prepare(
        ReadOnlySpan<byte> contact,
        ReadOnlySpan<byte> corroborated,
        byte[] residual
    )
    {
        int contactArea = 0,
            corroboratedArea = 0;
        for (var i = 0; i < contact.Length; i++)
        {
            residual[i] = (byte)(contact[i] != 0 && corroborated[i] == 0 ? 1 : 0);
            contactArea += contact[i];
            corroboratedArea += contact[i] != 0 && corroborated[i] != 0 ? 1 : 0;
        }
        return (contactArea, corroboratedArea);
    }

    private static Result<int, CaptureError> ExteriorComponents(
        byte[] residual,
        ReadOnlySpan<byte> feature,
        byte[] exterior,
        int[] queue,
        CancellationToken token
    )
    {
        var largest = 0;
        for (var index = 0; index < feature.Length; index++)
        {
            if ((index & 4095) == 0 && token.IsCancellationRequested)
                return Fs50MaskErrors.Cancelled;
            if (residual[index] == 0)
                continue;
            var count = Fs50ContactComponents.Flood(residual, queue, index);
            if (!ContainsAnyExterior(queue.AsSpan(0, count), feature))
                continue;
            largest = Math.Max(largest, count);
            for (var i = 0; i < count; i++)
                exterior[queue[i]] = 1;
        }
        return largest;
    }

    private static Result<Fs50WholeContactSpatialResult, CaptureError> Complete(
        byte[] exterior,
        ReadOnlySpan<byte> observedStatic,
        int[] queue,
        int contactArea,
        int corroboratedArea,
        int largest,
        int threshold,
        CancellationToken token
    )
    {
        int area = 0,
            staticArea = 0;
        for (var i = 0; i < observedStatic.Length; i++)
        {
            area += exterior[i];
            exterior[i] &= observedStatic[i];
            staticArea += exterior[i];
        }
        var largestStatic = 0;
        for (var i = 0; i < observedStatic.Length; i++)
        {
            if ((i & 4095) == 0 && token.IsCancellationRequested)
                return Fs50MaskErrors.Cancelled;
            if (exterior[i] != 0)
                largestStatic = Math.Max(
                    largestStatic,
                    Fs50ContactComponents.Flood(exterior, queue, i)
                );
        }
        return new Fs50WholeContactSpatialResult(
            new(contactArea, corroboratedArea, area, staticArea, area - staticArea, largest),
            largestStatic >= threshold
        );
    }

    private static bool ContainsAnyExterior(ReadOnlySpan<int> positions, ReadOnlySpan<byte> feature)
    {
        foreach (var index in positions)
        {
            int x = index % 800,
                y = index / 800;
            if (
                x == 0
                || y == 0
                || x == 799
                || y == 749
                || feature[index - 1] == 0
                || feature[index + 1] == 0
                || feature[index - 800] == 0
                || feature[index + 800] == 0
            )
                return true;
        }
        return false;
    }
}
