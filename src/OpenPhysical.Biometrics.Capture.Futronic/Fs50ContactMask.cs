// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Buffers;

internal static class Fs50ContactMask
{
    internal static bool WriteTexture(
        ReadOnlySpan<byte> pixels,
        Fs50EvidenceOptions options,
        byte[] strong,
        byte[] weak
    )
    {
        const int count = 815 * 765;
        var sums = ArrayPool<long>.Shared.Rent(count);
        var squares = ArrayPool<long>.Shared.Rent(count);
        try
        {
            _ = Fs50TextureStatistics.Fill(pixels, sums, squares, strong, weak, options);
            _ = Fs50PairedMaskMorphology.Apply(strong, weak, sums, options);
            for (var i = 0; i < pixels.Length; i++)
                weak[i] |= strong[i];
            return true;
        }
        finally
        {
            ArrayPool<long>.Shared.Return(sums, clearArray: true);
            ArrayPool<long>.Shared.Return(squares, clearArray: true);
        }
    }
}
