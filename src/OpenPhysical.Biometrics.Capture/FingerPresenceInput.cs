// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using System.Collections.Immutable;

internal static class FingerPresenceInput
{
    internal static CaptureError Invalid() =>
        new(
            CaptureErrorCodes.InvalidArgument,
            "A presence observation requires a matching image mask and foreground component count."
        );

    internal static bool Valid(CaptureFrame frame, ImmutableArray<byte> mask, int count) =>
        frame is not null
        && count >= 0
        && (
            mask.IsDefaultOrEmpty
            || (mask.Length == frame.Pixels.Length && mask.All(value => value <= 1))
        );
}
