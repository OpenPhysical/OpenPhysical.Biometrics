// SPDX-License-Identifier: GPL-2.0-or-later
// Adapted from FaceOFFx backdrop evidence; see Notices/FaceOFFx-MIT.txt.
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Imaging;

internal static class PortraitBackdrop
{
    private const int Width = 480;
    private const int Height = 640;

    internal static PortraitBackdropMeasurements Measure(
        byte[] rgb,
        byte[] face,
        Maybe<PortraitForegroundProtection> protection,
        PortraitBackdropOptions options
    )
    {
        var labels = Labels(face, protection);
        if (protection.HasValue)
            Classify(rgb, labels, options);
        var candidate = Mask(labels, 3);
        var stats = Statistics(rgb, candidate, options);
        var present =
            stats.PixelCount >= options.MinimumCandidatePixels
            && (double)stats.PixelCount / labels.Length >= options.MinimumCandidateFraction;
        if (!present)
        {
            for (var i = 0; i < labels.Length; i++)
                if (labels[i] == 3)
                    labels[i] = 0;
            Array.Clear(candidate);
            stats = Statistics(rgb, candidate, options);
        }
        var consistency = present ? Consistency(rgb, candidate, options) : 0;
        return new(
            present,
            (double)stats.PixelCount / labels.Length,
            consistency,
            stats,
            Sha256.HexDigest(candidate),
            options,
            Statistics(rgb, Mask(labels, 1), options),
            Statistics(rgb, Mask(labels, 2), options),
            Statistics(rgb, Mask(labels, 0), options)
        );
    }

    private static byte[] Labels(byte[] face, Maybe<PortraitForegroundProtection> protection)
    {
        var labels = new byte[face.Length];
        for (var i = 0; i < labels.Length; i++)
        {
            var x = i % Width;
            var y = i / Width;
            labels[i] =
                face[i] == 1 ? (byte)1
                : protection.Match(
                    p => y >= p.ChinY || x >= p.Left && x < p.Right && y >= p.Top && y < p.Bottom,
                    () => false
                )
                    ? (byte)2
                : (byte)0;
        }
        return labels;
    }

    private static bool Classify(byte[] rgb, byte[] labels, PortraitBackdropOptions options)
    {
        var eligible = new byte[labels.Length];
        var border = new List<int>();
        for (var i = 0; i < labels.Length; i++)
        {
            if (labels[i] != 0 || !PortraitBackdropTexture.Eligible(rgb, Width, Height, i, options))
                continue;
            eligible[i] = 1;
            if (i % Width == 0 || i % Width == Width - 1 || i < Width || i >= labels.Length - Width)
                border.Add(i);
        }
        if (border.Count == 0)
            return false;
        Flood(rgb, labels, eligible, border, Reference(rgb, border), options);
        return true;
    }

    private static byte[] Reference(byte[] rgb, List<int> border) =>
        Enumerable
            .Range(0, 3)
            .Select(c =>
                border
                    .Select(i => rgb[i * 3 + c])
                    .OrderBy(value => value)
                    .ElementAt(border.Count / 2)
            )
            .ToArray();

    private static bool Flood(
        byte[] rgb,
        byte[] labels,
        byte[] eligible,
        List<int> border,
        byte[] reference,
        PortraitBackdropOptions options
    )
    {
        var queue = new Queue<int>(border);
        while (queue.TryDequeue(out var i))
        {
            if (
                eligible[i] == 0
                || labels[i] != 0
                || !PortraitBackdropTexture.Matches(rgb, i, reference, options.MaximumColorDistance)
            )
                continue;
            labels[i] = 3;
            if (i % Width > 0)
                queue.Enqueue(i - 1);
            if (i % Width + 1 < Width)
                queue.Enqueue(i + 1);
            if (i >= Width)
                queue.Enqueue(i - Width);
            if (i + Width < labels.Length)
                queue.Enqueue(i + Width);
        }
        return true;
    }

    private static double Consistency(byte[] rgb, byte[] mask, PortraitBackdropOptions options)
    {
        var selected = Enumerable.Range(0, mask.Length).Where(i => mask[i] == 1).ToArray();
        double deviation = 0;
        for (var c = 0; c < 3; c++)
        {
            var median = selected
                .Select(i => rgb[i * 3 + c])
                .OrderBy(value => value)
                .ElementAt(selected.Length / 2);
            deviation += selected.Sum(i => Math.Abs(rgb[i * 3 + c] - median));
        }
        return Math.Clamp(
            1 - deviation / (selected.Length * 3 * Math.Max(1, options.MaximumColorDistance)),
            0,
            1
        );
    }

    private static byte[] Mask(byte[] labels, byte label) =>
        [.. labels.Select(value => value == label ? (byte)1 : (byte)0)];

    private static RgbRegionStatistics Statistics(
        byte[] rgb,
        byte[] mask,
        PortraitBackdropOptions options
    ) =>
        RgbRegionMeasurements
            .Measure(rgb, Width, Height, mask, options.EdgeDifferenceThreshold)
            .Value;
}
