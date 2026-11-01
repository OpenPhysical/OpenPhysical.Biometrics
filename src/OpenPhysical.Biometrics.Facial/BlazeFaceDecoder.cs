// SPDX-License-Identifier: Apache-2.0
// Adapted from yakhyo/mediapipe-face-mesh-onnx, copyright 2026 Yakhyokhuja Valikhujaev.
// Changes: C# implementation, immutable outputs, bounded buffers, and deterministic ownership.
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

// Decodes BlazeFace anchors into candidate rows [score, cx, cy, w, h, 6 x (x, y) keypoints].
internal static class BlazeFaceDecoder
{
    internal const int Anchors = 896;
    internal const int Values = 16;

    internal static List<double[]> Candidates(float[] raw, float[] logits)
    {
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
                var row = Row(raw, anchor, score, x, y, cells);
                if (row.All(double.IsFinite) && row[3] > 0 && row[4] > 0)
                    rows.Add(row);
            }
        return rows;
    }

    internal static Result<List<double[]>, FacialError> Suppress(List<double[]> candidates)
    {
        var remaining = candidates.OrderByDescending(r => r[0]).ToList();
        var result = new List<double[]>();
        while (remaining.Count > 0)
        {
            var top = remaining[0];
            var group = remaining.Where(r => Overlap(top, r) > .3).ToArray();
            if (group.Length == 0)
                return FacialErrors.DetectionGeometry().Fail<List<double[]>>();
            result.Add(Blend(top, group));
            remaining = remaining.Where(r => Overlap(top, r) <= .3).ToList();
        }
        return result.Succeed();
    }

    private static double[] Row(float[] raw, int anchor, double score, int x, int y, int cells)
    {
        var row = new double[17];
        row[0] = score;
        for (var k = 0; k < Values; k++)
            row[k + 1] =
                raw[anchor * Values + k] / 128.0
                + (k is 2 or 3 ? 0 : ((k % 2 == 0 ? x : y) + .5) / cells);
        return row;
    }

    private static double[] Blend(double[] top, double[][] group)
    {
        var total = group.Sum(r => r[0]);
        var blend = (double[])top.Clone();
        for (var k = 1; k < 17; k++)
            blend[k] = group.Sum(r => r[k] * r[0]) / total;
        return blend;
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

    internal static double Sigmoid(double value) =>
        value >= 0 ? 1 / (1 + Math.Exp(-value)) : Math.Exp(value) / (1 + Math.Exp(value));
}
