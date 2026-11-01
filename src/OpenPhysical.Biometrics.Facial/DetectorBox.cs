// SPDX-License-Identifier: Apache-2.0
// Adapted from yakhyo/mediapipe-face-mesh-onnx, copyright 2026 Yakhyokhuja Valikhujaev.
// Changes: C# implementation, immutable outputs, bounded buffers, and deterministic ownership.
namespace OpenPhysical.Biometrics.Facial;

// Letterbox placement of the source inside the 128 by 128 detector input.
internal sealed record DetectorBox(double Scale, double X, double Y)
{
    internal const int Size = 128;

    internal PortraitTransform Transform => new(Scale, 0, X, Y);

    internal static DetectorBox For(PortraitPixels image)
    {
        var scale = (double)Size / Math.Max(image.Width, image.Height);
        return new(scale, (Size - image.Width * scale) / 2, (Size - image.Height * scale) / 2);
    }

    // Maps a normalized detector coordinate into source pixels.
    internal double SourceX(double normalized) => (normalized * Size - X) / Scale;

    internal double SourceY(double normalized) => (normalized * Size - Y) / Scale;
}
