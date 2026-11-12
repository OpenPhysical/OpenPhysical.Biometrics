// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using CSharpFunctionalExtensions;

internal static class FingerprintClahe
{
    internal static Result<byte[], CaptureError> Apply(
        CaptureFrame original,
        FingerprintProcessingMask mask,
        FingerprintPreprocessingPolicy policy,
        CancellationToken cancellationToken
    )
    {
        var columns = (original.Width + policy.TileSize - 1) / policy.TileSize;
        var rows = (original.Height + policy.TileSize - 1) / policy.TileSize;
        var lookup = new byte[columns * rows * 256];
        try
        {
            for (var y = 0; y < rows; y++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return new CaptureError(
                        CaptureErrorCodes.Cancelled,
                        "Image preparation was cancelled."
                    );
                for (var x = 0; x < columns; x++)
                    _ = FingerprintClaheHistogram.Fill(
                        original.Pixels.AsSpan(),
                        mask,
                        policy,
                        x,
                        y,
                        lookup.AsSpan(((y * columns) + x) * 256, 256)
                    );
            }
            return Map(original, lookup, columns, policy, cancellationToken);
        }
        finally
        {
            Array.Clear(lookup);
        }
    }

    private static Result<byte[], CaptureError> Map(
        CaptureFrame original,
        byte[] lookup,
        int columns,
        FingerprintPreprocessingPolicy policy,
        CancellationToken cancellationToken
    )
    {
        var horizontal = FingerprintClaheAxis.Create(original.Width, policy.TileSize);
        var vertical = FingerprintClaheAxis.Create(original.Height, policy.TileSize);
        var output = new byte[original.Pixels.Length];
        for (var y = 0; y < original.Height; y++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Array.Clear(output);
                return new CaptureError(
                    CaptureErrorCodes.Cancelled,
                    "Image preparation was cancelled."
                );
            }
            for (var x = 0; x < original.Width; x++)
            {
                var index = (y * original.Width) + x;
                var value = original.Pixels[index];
                var equalized = Interpolate(lookup, columns, horizontal[x], vertical[y], value);
                output[index] = (byte)
                    Math.Clamp(Math.Round(value + (policy.Blend * (equalized - value))), 0, 255);
            }
        }
        return output;
    }

    private static double Interpolate(
        byte[] lookup,
        int columns,
        FingerprintClaheAxis x,
        FingerprintClaheAxis y,
        byte value
    )
    {
        double topLeft = lookup[(((y.Lower * columns) + x.Lower) * 256) + value];
        double topRight = lookup[(((y.Lower * columns) + x.Upper) * 256) + value];
        double bottomLeft = lookup[(((y.Upper * columns) + x.Lower) * 256) + value];
        double bottomRight = lookup[(((y.Upper * columns) + x.Upper) * 256) + value];
        double top = topLeft + (x.Weight * (topRight - topLeft));
        double bottom = bottomLeft + (x.Weight * (bottomRight - bottomLeft));
        return top + (y.Weight * (bottom - top));
    }
}
