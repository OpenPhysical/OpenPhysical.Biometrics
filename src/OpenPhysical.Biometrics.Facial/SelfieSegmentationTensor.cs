// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using Microsoft.ML.OnnxRuntime.Tensors;

// Google's square model stretches RGB to 256x256, using pixel centers and [0,1] NHWC floats.
internal static class SelfieSegmentationTensor
{
    internal static DenseTensor<float> Create(PortraitImage image)
    {
        const int size = 256;
        var tensor = new DenseTensor<float>([1, size, size, 3]);
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var sx = Math.Clamp((x + .5) * image.Width / size - .5, 0, image.Width - 1);
            var sy = Math.Clamp((y + .5) * image.Height / size - .5, 0, image.Height - 1);
            for (var channel = 0; channel < 3; channel++)
                tensor[0, y, x, channel] = Sample(image, sx, sy, channel) / 255f;
        }
        return tensor;
    }

    private static float Sample(PortraitImage image, double x, double y, int channel)
    {
        var left = (int)x;
        var top = (int)y;
        var right = Math.Min(left + 1, image.Width - 1);
        var bottom = Math.Min(top + 1, image.Height - 1);
        var rgb = image.RgbPixels;
        var upper =
            rgb[(top * image.Width + left) * 3 + channel] * (1 - (x - left))
            + rgb[(top * image.Width + right) * 3 + channel] * (x - left);
        var lower =
            rgb[(bottom * image.Width + left) * 3 + channel] * (1 - (x - left))
            + rgb[(bottom * image.Width + right) * 3 + channel] * (x - left);
        return (float)(upper * (1 - (y - top)) + lower * (y - top));
    }
}
