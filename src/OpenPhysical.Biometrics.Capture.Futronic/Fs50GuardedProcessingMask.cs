// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class Fs50GuardedProcessingMask
{
    internal static Result<FingerprintProcessingMask, CaptureError> Create(
        ImmutableArray<byte> retained,
        int guard
    )
    {
        if (guard is < 0 or > 32)
            return new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Artificial-cut guard is outside zero through 32 pixels."
            );
        if (retained.Length != 800 * 750)
            return new CaptureError(
                CaptureErrorCodes.UnsupportedGeometry,
                "A guarded processing mask requires the complete FS50 plane."
            );
        if (guard == 0)
            return FingerprintProcessingMask.Create(800, 750, retained.AsSpan(), retained.AsSpan());
        var source = retained.ToArray();
        var inner = Fs50MaskMorphology.Inner(source, guard);
        try
        {
            return FingerprintProcessingMask.Create(800, 750, retained.AsSpan(), inner);
        }
        finally
        {
            Array.Clear(source);
            Array.Clear(inner);
        }
    }
}
