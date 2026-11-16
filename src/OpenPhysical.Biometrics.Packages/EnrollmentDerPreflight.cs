// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

internal static class EnrollmentDerPreflight
{
    private const int MaximumDepth = 16;
    private const int MaximumElements = 2_048;

    internal static bool IsSafe(ReadOnlySpan<byte> input)
    {
        var offset = 0;
        var elements = 0;
        return ReadElement(input, ref offset, input.Length, 0, ref elements)
            && offset == input.Length;
    }

    private static bool ReadElement(
        ReadOnlySpan<byte> input,
        ref int offset,
        int parentEnd,
        int depth,
        ref int elements
    )
    {
        if (depth > MaximumDepth || ++elements > MaximumElements || offset >= parentEnd)
            return false;
        var tag = input[offset++];
        if (tag is not (0x30 or 0x02 or 0x04 or 0x0c))
            return false;
        if (!ReadLength(input, ref offset, parentEnd, out var length))
            return false;
        if (length > parentEnd - offset)
            return false;
        var end = offset + length;
        if (tag != 0x30)
            offset = end;
        else
        {
            while (offset < end)
            {
                if (!ReadElement(input, ref offset, end, depth + 1, ref elements))
                    return false;
            }
        }
        return offset == end;
    }

    private static bool ReadLength(
        ReadOnlySpan<byte> input,
        ref int offset,
        int parentEnd,
        out int length
    )
    {
        length = 0;
        if (offset >= parentEnd)
            return false;
        var first = input[offset++];
        if (first < 0x80)
        {
            length = first;
            return true;
        }
        var octets = first & 0x7f;
        if (octets is 0 or > 4 || offset + octets > parentEnd || input[offset] == 0)
            return false;
        for (var i = 0; i < octets; i++)
        {
            if (length > (int.MaxValue >> 8))
                return false;
            length = (length << 8) | input[offset++];
        }
        return length >= 128;
    }
}
