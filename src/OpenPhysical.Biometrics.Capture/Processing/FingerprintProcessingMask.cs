// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Owned row-major binary retained/inner planes in unchanged image coordinates.</summary>
public sealed class FingerprintProcessingMask
{
    private FingerprintProcessingMask(
        int width,
        int height,
        ReadOnlySpan<byte> retained,
        ReadOnlySpan<byte> inner
    )
    {
        Width = width;
        Height = height;
        Retained = ImmutableArray.CreateRange(retained.ToArray());
        Inner = ImmutableArray.CreateRange(inner.ToArray());
        Digest = CaptureProcessingDigest.Mask(width, height, retained, inner);
    }

    /// <summary>Gets the image width.</summary>
    public int Width { get; }

    /// <summary>Gets the image height.</summary>
    public int Height { get; }

    /// <summary>Gets retained image pixels, one included and zero excluded.</summary>
    public ImmutableArray<byte> Retained { get; }

    /// <summary>Gets the guarded area permitted to retain extracted minutiae.</summary>
    public ImmutableArray<byte> Inner { get; }

    /// <summary>Gets SHA-256 bound to geometry and both ordered mask planes.</summary>
    public string Digest { get; }

    /// <summary>Validates mask geometry, polarity and inner-area containment before copying.</summary>
    /// <param name="width">Image width.</param>
    /// <param name="height">Image height.</param>
    /// <param name="retained">Complete retained binary plane.</param>
    /// <param name="inner">Complete inner binary plane contained in retained.</param>
    /// <returns>An owned mask or precise validation failure.</returns>
    public static Result<FingerprintProcessingMask, CaptureError> Create(
        int width,
        int height,
        ReadOnlySpan<byte> retained,
        ReadOnlySpan<byte> inner
    )
    {
        var geometry = CaptureGeometry.Create(width, height);
        if (geometry.IsFailure)
            return geometry.Error;
        if (retained.Length != geometry.Value.PixelCount || inner.Length != retained.Length)
            return new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Processing masks must match the complete image geometry."
            );
        for (var i = 0; i < retained.Length; i++)
            if (retained[i] > 1 || inner[i] > retained[i])
                return new CaptureError(
                    CaptureErrorCodes.InvalidArgument,
                    "Processing masks require binary polarity and an inner area contained in the retained area."
                );
        return new FingerprintProcessingMask(width, height, retained, inner);
    }
}
