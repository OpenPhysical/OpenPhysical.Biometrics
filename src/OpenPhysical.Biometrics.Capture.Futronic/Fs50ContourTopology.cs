// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Buffers;
using CSharpFunctionalExtensions;

internal static class Fs50ContourTopology
{
    internal static UnitResult<CaptureError> Connected(byte[] contact)
    {
        var working = contact.ToArray();
        var queue = ArrayPool<int>.Shared.Rent(contact.Length);
        try
        {
            var start = Array.IndexOf(working, (byte)1);
            return
                start >= 0
                && Fs50ContactComponents.Flood(working, queue, start) == contact.Sum(value => value)
                ? UnitResult.Success<CaptureError>()
                : UnitResult.Failure(
                    new CaptureError(
                        CaptureErrorCodes.InvalidArgument,
                        "Contact contour requires one nonempty eight-connected component."
                    )
                );
        }
        finally
        {
            Array.Clear(working);
            ArrayPool<int>.Shared.Return(queue, clearArray: true);
        }
    }

    internal static Result<byte[], CaptureError> Fill(
        ReadOnlySpan<byte> contact,
        CancellationToken token
    )
    {
        var exterior = new byte[contact.Length];
        var queue = ArrayPool<int>.Shared.Rent(contact.Length);
        try
        {
            var tail = Seed(contact, exterior, queue);
            for (var head = 0; head < tail; head++)
            {
                if ((head & 4095) == 0 && token.IsCancellationRequested)
                    return Fs50MaskErrors.Cancelled;
                var index = queue[head];
                int x = index % 800,
                    y = index / 800;
                if (x > 0)
                    tail = Visit(index - 1, contact, exterior, queue, tail);
                if (x < 799)
                    tail = Visit(index + 1, contact, exterior, queue, tail);
                if (y > 0)
                    tail = Visit(index - 800, contact, exterior, queue, tail);
                if (y < 749)
                    tail = Visit(index + 800, contact, exterior, queue, tail);
            }
            for (var i = 0; i < exterior.Length; i++)
                exterior[i] = (byte)(exterior[i] == 0 ? 1 : 0);
            return token.IsCancellationRequested ? Fs50MaskErrors.Cancelled : exterior;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(queue, clearArray: true);
        }
    }

    private static int Seed(ReadOnlySpan<byte> contact, byte[] exterior, int[] queue)
    {
        var tail = 0;
        for (var x = 0; x < 800; x++)
        {
            tail = Visit(x, contact, exterior, queue, tail);
            tail = Visit(749 * 800 + x, contact, exterior, queue, tail);
        }
        for (var y = 1; y < 749; y++)
        {
            tail = Visit(y * 800, contact, exterior, queue, tail);
            tail = Visit(y * 800 + 799, contact, exterior, queue, tail);
        }
        return tail;
    }

    private static int Visit(
        int index,
        ReadOnlySpan<byte> contact,
        byte[] exterior,
        int[] queue,
        int tail
    )
    {
        if (contact[index] != 0 || exterior[index] != 0)
            return tail;
        exterior[index] = 1;
        queue[tail] = index;
        return tail + 1;
    }
}
