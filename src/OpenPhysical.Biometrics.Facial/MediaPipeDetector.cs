// SPDX-License-Identifier: Apache-2.0
// Adapted from yakhyo/mediapipe-face-mesh-onnx, copyright 2026 Yakhyokhuja Valikhujaev.
// Changes: C# implementation, immutable outputs, bounded buffers, and deterministic ownership.
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using System.Security.Cryptography;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenPhysical.Biometrics.Onnx;

internal sealed class MediaPipeDetector : IDisposable
{
    private readonly InferenceSession detector;
    private readonly InferenceSession mesh;

    internal MediaPipeDetector()
    {
        using var options = new SessionOptions { IntraOpNumThreads = 1, InterOpNumThreads = 1 };
        detector = new InferenceSession(
            Load(
                "face_detection_short_range.onnx",
                "2f2689b040becf555706d2cb978d2f0e3296ea82413734fba9a856c66c5f2b17"
            ),
            options
        );
        try
        {
            mesh = new InferenceSession(
                Load(
                    "face_landmarker_Nx3x256x256.onnx",
                    "111795f8703cdeb6d0c68a9f3cc966a0f23f8786bb00f4577a11f461fc4276ac"
                ),
                options
            );
        }
        catch
        {
            detector.Dispose();
            throw;
        }
    }

    internal static byte[] Load(string file, string hash)
    {
        using var stream =
            typeof(MediaPipeModels).Assembly.GetManifestResourceStream(
                "OpenPhysical.Biometrics.Onnx.Resources." + file
            ) ?? throw new InvalidOperationException("The packaged MediaPipe model is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        if (
            !Convert
                .ToHexString(SHA256.HashData(bytes))
                .Equals(hash, StringComparison.OrdinalIgnoreCase)
        )
            throw new InvalidOperationException(
                "The packaged MediaPipe model checksum is invalid."
            );
        return bytes;
    }

    internal ImmutableArray<PortraitFace> Detect(PortraitPixels image)
    {
        var scale = 128.0 / Math.Max(image.Width, image.Height);
        var px = (128 - image.Width * scale) / 2;
        var py = (128 - image.Height * scale) / 2;
        var detectorPixels = image.Warp(128, 128, new(scale, 0, px, py), true);
        using var outputs = detector.Run([
            NamedOnnxValue.CreateFromTensor("input", Tensor(detectorPixels, 128, true)),
        ]);
        var raw = outputs.First().AsTensor<float>().ToArray();
        var logits = outputs.Last().AsTensor<float>().ToArray();
        if (raw.Length != 896 * 16 || logits.Length != 896)
            throw new InvalidOperationException("Unexpected detector output dimensions.");
        var rows = new List<double[]>();
        var anchor = 0;
        foreach (var (cells, repetitions) in new[] { (16, 2), (8, 6) })
            for (var y = 0; y < cells; y++)
            for (var x = 0; x < cells; x++)
            for (var repeat = 0; repeat < repetitions; repeat++, anchor++)
            {
                var score = Sigmoid(logits[anchor]);
                if (score < .5)
                    continue;
                var row = new double[17];
                row[0] = score;
                for (var k = 0; k < 16; k++)
                    row[k + 1] =
                        raw[anchor * 16 + k] / 128.0
                        + (k is 2 or 3 ? 0 : ((k % 2 == 0 ? x : y) + .5) / cells);
                if (row.All(double.IsFinite) && row[3] > 0 && row[4] > 0)
                    rows.Add(row);
            }
        var blended = Suppress(rows);
        var faces = new List<(double X, double Y, PortraitFace Face)>();
        foreach (var row in blended)
        {
            var cx = (row[1] * 128 - px) / scale;
            var cy = (row[2] * 128 - py) / scale;
            var side = Math.Max(row[3], row[4]) * 128 / scale * 1.5;
            var angle = Math.Atan2(row[8] - row[6], row[7] - row[5]);
            var a = Math.Cos(angle) * 256 / side;
            var b = Math.Sin(angle) * 256 / side;
            var transform = new PortraitTransform(
                a,
                b,
                128 - a * cx - b * cy,
                128 + b * cx - a * cy
            );
            var pixels = image.Warp(256, 256, transform, true);
            using var result = mesh.Run([
                NamedOnnxValue.CreateFromTensor("input", Tensor(pixels, 256, false)),
            ]);
            var values = result.First(o => o.Name == "landmarks").AsTensor<float>().ToArray();
            var presence = Sigmoid(result.First(o => o.Name == "score").AsTensor<float>().First());
            if (values.Length != 1434 || !values.All(float.IsFinite) || !double.IsFinite(presence))
                throw new InvalidOperationException("Invalid landmark output.");
            if (presence < .5)
                continue;
            var points = Enumerable
                .Range(0, 478)
                .Select(i =>
                {
                    var point = transform.Inverse(new(values[i * 3], values[i * 3 + 1]));
                    return new FaceMeshPoint(point.X, point.Y, values[i * 3 + 2] * side / 256);
                })
                .ToImmutableArray();
            PortraitPoint Ear(int k) =>
                new((row[5 + k * 2] * 128 - px) / scale, (row[6 + k * 2] * 128 - py) / scale);
            faces.Add((cx, cy, new(0, row[0], presence, points, Ear(4), Ear(5))));
        }
        return faces
            .OrderBy(f => f.X)
            .ThenBy(f => f.Y)
            .Select((f, i) => f.Face with { Index = i })
            .ToImmutableArray();
    }

    private static DenseTensor<float> Tensor(byte[] rgb, int size, bool signed)
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

    private static List<double[]> Suppress(List<double[]> candidates)
    {
        var remaining = candidates.OrderByDescending(r => r[0]).ToList();
        var result = new List<double[]>();
        while (remaining.Count > 0)
        {
            var top = remaining[0];
            var group = remaining.Where(r => Overlap(top, r) > .3).ToArray();
            if (group.Length == 0)
                throw new InvalidOperationException("Invalid detection geometry.");
            var total = group.Sum(r => r[0]);
            var blend = (double[])top.Clone();
            for (var k = 1; k < 17; k++)
                blend[k] = group.Sum(r => r[k] * r[0]) / total;
            result.Add(blend);
            remaining = remaining.Where(r => Overlap(top, r) <= .3).ToList();
        }
        return result;
    }

    private static double Overlap(double[] a, double[] b)
    {
        var width = Math.Max(
            0,
            Math.Min(a[1] + a[3] / 2, b[1] + b[3] / 2) - Math.Max(a[1] - a[3] / 2, b[1] - b[3] / 2)
        );
        var height = Math.Max(
            0,
            Math.Min(a[2] + a[4] / 2, b[2] + b[4] / 2) - Math.Max(a[2] - a[4] / 2, b[2] - b[4] / 2)
        );
        var area = width * height;
        return area / Math.Max(1e-9, a[3] * a[4] + b[3] * b[4] - area);
    }

    private static double Sigmoid(double value) =>
        value >= 0 ? 1 / (1 + Math.Exp(-value)) : Math.Exp(value) / (1 + Math.Exp(value));

    public void Dispose()
    {
        mesh.Dispose();
        detector.Dispose();
    }
}
