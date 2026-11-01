// SPDX-License-Identifier: Apache-2.0
// Adapted from yakhyo/mediapipe-face-mesh-onnx, copyright 2026 Yakhyokhuja Valikhujaev.
namespace OpenPhysical.Biometrics.Facial;

using Microsoft.ML.OnnxRuntime.Tensors;

// Converts interleaved RGB bytes to the planar float layout the MediaPipe networks expect.
internal static class MediaPipeTensor
{
    internal static DenseTensor<float> Create(byte[] rgb, int size, bool signed)
    {
        var plane = size * size;
        var values = new float[plane * 3];
        for (var i = 0; i < plane; i++)
        for (var c = 0; c < 3; c++)
            values[c * plane + i] = signed
                ? (rgb[i * 3 + c] - 127.5f) / 127.5f
                : rgb[i * 3 + c] / 255f;
        return new(values, [1, 3, size, size]);
    }
}
