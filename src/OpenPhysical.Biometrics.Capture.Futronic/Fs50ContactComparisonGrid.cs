// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Buffers;
using CSharpFunctionalExtensions;

internal sealed class Fs50ContactComparisonGrid : IDisposable
{
    private const int Stride = 801;
    private const int Count = Stride * 752;
    private readonly int[] counts = Rent();
    private readonly int[] differences = Rent();
    private readonly int[] changed = Rent();
    private readonly int[] unchanged = Rent();

    internal UnitResult<CaptureError> Fill(
        ReadOnlySpan<byte> pixels,
        ReadOnlySpan<byte> baseline,
        ReadOnlySpan<byte> contact,
        CancellationToken token
    )
    {
        for (var y = 0; y < 750; y++)
        {
            if (token.IsCancellationRequested)
                return Fs50MaskErrors.Cancelled;
            int occupied = 0,
                difference = 0;
            for (var x = 0; x < 800; x++)
            {
                var input = y * 800 + x;
                if (contact[input] != 0)
                {
                    occupied++;
                    difference += Math.Abs(pixels[input] - baseline[input]);
                }
                var next = (y + 1) * Stride + x + 1;
                counts[next] = counts[next - Stride] + occupied;
                differences[next] = differences[next - Stride] + difference;
            }
        }
        return UnitResult.Success<CaptureError>();
    }

    internal UnitResult<CaptureError> Observe(
        Fs50WholeContactPolicy policy,
        CancellationToken token
    )
    {
        var size = policy.Options.WindowSize;
        var half = size / 2;
        for (var y = 0; y < 750; y += policy.Options.GridStep)
        {
            if (token.IsCancellationRequested)
                return Fs50MaskErrors.Cancelled;
            for (var x = 0; x < 800; x += policy.Options.GridStep)
            {
                int left = Math.Max(0, x - half),
                    top = Math.Max(0, y - half);
                int right = Math.Min(800, x - half + size),
                    bottom = Math.Min(750, y - half + size);
                var occupied = Sum(counts, left, top, right, bottom);
                if (
                    occupied == 0
                    || occupied
                        < (right - left)
                            * (bottom - top)
                            * policy.Evidence.Options.MinimumBlockCoverage
                )
                    continue;
                var difference = (double)Sum(differences, left, top, right, bottom) / occupied;
                if (
                    difference
                    > policy.EmptySensor.Options.MinimumPresentationMeanAbsoluteDifference
                )
                    Mark(changed, left, top, right, bottom);
                else if (
                    difference <= policy.EmptySensor.Options.MaximumStableMeanAbsoluteDifference
                )
                    Mark(unchanged, left, top, right, bottom);
            }
        }
        return UnitResult.Success<CaptureError>();
    }

    internal Result<Fs50WholeContactWindowEvidence, CaptureError> Finish(
        ReadOnlySpan<byte> contact,
        CancellationToken token
    )
    {
        var result = new Fs50WholeContactWindowEvidence();
        var changedOutput = result.Changed.AsSpan(0, contact.Length);
        var staticOutput = result.StaticOutput;
        for (var y = 0; y < 750; y++)
        {
            if (token.IsCancellationRequested)
            {
                result.Dispose();
                return Fs50MaskErrors.Cancelled;
            }
            int changedRow = 0,
                unchangedRow = 0;
            var row = (y + 1) * Stride;
            for (var x = 0; x < 800; x++)
            {
                var index = y * 800 + x;
                var cell = row + x;
                changedRow += changed[cell];
                unchangedRow += unchanged[cell];
                var a = changed[cell] = changedRow + changed[cell - Stride];
                var b = unchanged[cell] = unchangedRow + unchanged[cell - Stride];
                changedOutput[index] = (byte)(contact[index] != 0 && a > 0 ? 1 : 0);
                staticOutput[index] = (byte)(contact[index] != 0 && b > 0 ? 1 : 0);
            }
        }
        return result;
    }

    public void Dispose()
    {
        foreach (var array in new[] { counts, differences, changed, unchanged })
            ArrayPool<int>.Shared.Return(array, clearArray: true);
    }

    private static int Sum(int[] integral, int left, int top, int right, int bottom) =>
        integral[bottom * Stride + right]
        - integral[top * Stride + right]
        - integral[bottom * Stride + left]
        + integral[top * Stride + left];

    private static bool Mark(int[] plane, int left, int top, int right, int bottom)
    {
        plane[(top + 1) * Stride + left]++;
        plane[(top + 1) * Stride + right]--;
        plane[(bottom + 1) * Stride + left]--;
        plane[(bottom + 1) * Stride + right]++;
        return true;
    }

    private static int[] Rent()
    {
        var result = ArrayPool<int>.Shared.Rent(Count);
        Array.Clear(result, 0, Count);
        return result;
    }
}
