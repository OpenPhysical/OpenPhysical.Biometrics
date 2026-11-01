// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

internal sealed record PortraitPixels(int Width, int Height, byte[] Rgb)
{
    internal Result<byte[], FacialError> Warp(
        int width,
        int height,
        PortraitTransform transform,
        bool quantize = false
    )
    {
        if (!transform.IsInvertible)
            return FacialErrors.SingularTransform().Fail<byte[]>();
        var output = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var source = transform.InverseUnchecked(new(x, y));
            // OpenCV's reference model sampler quantizes bilinear coefficients to 1/32.
            var sx = quantize ? Math.Round(source.X * 32, MidpointRounding.ToEven) / 32 : source.X;
            var sy = quantize ? Math.Round(source.Y * 32, MidpointRounding.ToEven) / 32 : source.Y;
            for (var c = 0; c < 3; c++)
                output[(y * width + x) * 3 + c] = Sample(sx, sy, c);
        }
        return output.Succeed();
    }

    private byte Sample(double sx, double sy, int channel)
    {
        var ix = (int)Math.Floor(sx);
        var iy = (int)Math.Floor(sy);
        var fx = sx - ix;
        var fy = sy - iy;
        double sum = 0;
        for (var j = 0; j < 2; j++)
        for (var k = 0; k < 2; k++)
            if (ix + k >= 0 && ix + k < Width && iy + j >= 0 && iy + j < Height)
                sum +=
                    Rgb[((iy + j) * Width + ix + k) * 3 + channel]
                    * (k == 0 ? 1 - fx : fx)
                    * (j == 0 ? 1 - fy : fy);
        return (byte)Math.Clamp(Math.Floor(sum + .5), 0, 255);
    }
}
