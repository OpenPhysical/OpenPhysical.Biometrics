// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Imaging;

internal sealed record ImageDecodePolicy(
    bool Srgb,
    bool Orientation,
    bool Opaque,
    long MaximumPixels
)
{
    internal static ImageDecodePolicy General { get; } =
        new(false, false, false, RgbImage.MaximumPixels);
    internal static ImageDecodePolicy Portrait { get; } = new(true, true, true, 32 * 1024 * 1024);
}
