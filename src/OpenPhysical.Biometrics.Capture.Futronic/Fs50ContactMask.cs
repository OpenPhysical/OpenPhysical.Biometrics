// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

internal static class Fs50ContactMask
{
    internal static byte[] Texture(ReadOnlySpan<byte> pixels)
    {
        const int width = Fs50Descriptor.Width;
        const int height = Fs50Descriptor.Height;
        const int stride = width + 15;
        var sums = new long[stride * (height + 15)];
        var squares = new long[sums.Length];
        for (int y = 0; y < height + 14; y++)
        {
            long sum = 0,
                square = 0;
            for (int x = 0; x < width + 14; x++)
            {
                var value = pixels[(Reflect(y - 7, height) * width) + Reflect(x - 7, width)];
                sum += value;
                square += value * value;
                var index = ((y + 1) * stride) + x + 1;
                sums[index] = sums[index - stride] + sum;
                squares[index] = squares[index - stride] + square;
            }
        }
        var mask = new byte[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            double mean = Rectangle(sums, stride, x, y, x + 15, y + 15) / 225.0;
            double variance =
                Rectangle(squares, stride, x, y, x + 15, y + 15) / 225.0 - (mean * mean);
            mask[(y * width) + x] = (byte)(variance > 90 && mean < 242 ? 1 : 0);
        }
        return Morph(Morph(Morph(Morph(mask, 6, true), 6, false), 3, false), 3, true);
    }

    private static byte[] Morph(byte[] mask, int radius, bool dilate)
    {
        const int width = Fs50Descriptor.Width;
        const int height = Fs50Descriptor.Height;
        const int stride = width + 1;
        var integral = new long[stride * (height + 1)];
        for (int y = 0; y < height; y++)
        {
            long sum = 0;
            for (int x = 0; x < width; x++)
            {
                sum += mask[(y * width) + x];
                integral[((y + 1) * stride) + x + 1] = integral[(y * stride) + x + 1] + sum;
            }
        }
        var output = new byte[mask.Length];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int left = Math.Max(0, x - radius),
                top = Math.Max(0, y - radius);
            int right = Math.Min(width, x + radius + 1),
                bottom = Math.Min(height, y + radius + 1);
            long count = Rectangle(integral, stride, left, top, right, bottom);
            output[(y * width) + x] = (byte)(
                (dilate ? count > 0 : count == (right - left) * (bottom - top)) ? 1 : 0
            );
        }
        return output;
    }

    private static long Rectangle(
        long[] values,
        int stride,
        int left,
        int top,
        int right,
        int bottom
    ) =>
        values[(bottom * stride) + right]
        - values[(top * stride) + right]
        - values[(bottom * stride) + left]
        + values[(top * stride) + left];

    private static int Reflect(int index, int size) =>
        index < 0 ? -index
        : index >= size ? (2 * size) - index - 2
        : index;
}
