// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>The sensor/output geometry supported by the observed FS50 protocol.</summary>
public static class Fs50Descriptor
{
    /// <summary>Raw sensor width.</summary>
    public const int RawWidth = 1280;

    /// <summary>Raw sensor height.</summary>
    public const int RawHeight = 832;

    /// <summary>Factory-corrected output width, without a physical scale assertion.</summary>
    public const int Width = 800;

    /// <summary>Factory-corrected output height.</summary>
    public const int Height = 750;

    /// <summary>Checks the exact observed orientation and correction version.</summary>
    /// <param name="descriptor">The complete 64-byte descriptor.</param>
    /// <returns>Success or an unsupported/incomplete geometry error.</returns>
    public static UnitResult<CaptureError> Validate(ReadOnlySpan<byte> descriptor)
    {
        if (descriptor.Length != 64)
        {
            return UnitResult.Failure(
                new CaptureError(
                    CaptureErrorCodes.TransferIncomplete,
                    "Incomplete FS50 descriptor."
                )
            );
        }

        return
            descriptor.Slice(4, 6).SequenceEqual(new byte[] { 5, 0, 3, 64, 0, 0 })
            && descriptor.Slice(34, 5).SequenceEqual(new byte[] { 3, 32, 2, 238, 143 })
            ? UnitResult.Success<CaptureError>()
            : UnitResult.Failure(
                new CaptureError(
                    CaptureErrorCodes.UnsupportedGeometry,
                    "Unsupported FS50 geometry or correction version."
                )
            );
    }
}
