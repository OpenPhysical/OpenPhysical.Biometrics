// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Imaging.Tests;

/// <summary>Describes one single-strip little-endian TIFF used by decoder tests.</summary>
/// <param name="Samples">Samples per pixel.</param>
/// <param name="Photometric">Photometric interpretation tag value.</param>
/// <param name="Compression">Compression tag value.</param>
/// <param name="ExtraSample">Optional ExtraSamples tag value.</param>
/// <param name="Pixels">Interleaved strip bytes for a one-pixel image.</param>
/// <param name="StripLength">Optional StripByteCounts override.</param>
internal sealed record TiffSpec(
    ushort Samples,
    ushort Photometric,
    ushort Compression,
    Maybe<ushort> ExtraSample,
    byte[] Pixels,
    Maybe<uint> StripLength
)
{
    /// <summary>Gets an RGBA pixel with no ExtraSamples tag.</summary>
    internal static TiffSpec Rgba { get; } =
        new(4, 2, 1, Maybe<ushort>.None, [60, 30, 15, 128], Maybe<uint>.None);

    /// <summary>Gets an RGB pixel.</summary>
    internal static TiffSpec Rgb { get; } =
        new(3, 2, 1, Maybe<ushort>.None, [60, 30, 15], Maybe<uint>.None);

    /// <summary>Gets a grayscale pixel with white-is-zero interpretation.</summary>
    internal static TiffSpec Gray { get; } =
        new(1, 0, 1, Maybe<ushort>.None, [200], Maybe<uint>.None);
}
