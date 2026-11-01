// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Imaging;

// The compatibility export uses one uncompressed RGB strip. Keeping this small
// codec local makes the existing TIFF workflow independent of optional encoders.
internal static class BaselineTiff
{
    internal static bool IsTiff(ReadOnlySpan<byte> data) =>
        data.Length >= 4
        && (data[..4].SequenceEqual("II*\0"u8) || data[..4].SequenceEqual("MM\0*"u8));
}
