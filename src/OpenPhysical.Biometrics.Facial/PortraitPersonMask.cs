// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>An immutable person-confidence mask bound to unchanged source-image coordinates.</summary>
/// <remarks>A segmentation estimate does not establish facial identity or PIV image conformance.</remarks>
public sealed class PortraitPersonMask
{
    internal PortraitPersonMask(PortraitImage source, ImmutableArray<float> confidence)
    {
        SourceSha256 = source.SourceSha256;
        SourceWidth = source.Width;
        SourceHeight = source.Height;
        Confidence = confidence;
    }

    /// <summary>Identifies the exact immutable input image.</summary>
    public string SourceSha256 { get; }

    /// <summary>Gets the unchanged source width in pixels.</summary>
    public int SourceWidth { get; }

    /// <summary>Gets the unchanged source height in pixels.</summary>
    public int SourceHeight { get; }

    /// <summary>Gets the native mask width.</summary>
    public int Width => 256;

    /// <summary>Gets the native mask height.</summary>
    public int Height => 256;

    /// <summary>Gets row-major probabilities that each native mask pixel belongs to a person.</summary>
    public ImmutableArray<float> Confidence { get; }

    /// <summary>Identifies the embedded, hash-verified converted Google model.</summary>
    public string ModelSha256 => SelfieSegmentationModel.Sha256;

    /// <summary>Identifies the stretch-resize and pixel-center sampling convention.</summary>
    public string PolicyId => "google-selfie-square-bilinear-v1";

    /// <summary>Samples person confidence at a source pixel center, with top-left origin.</summary>
    /// <param name="sourcePoint">Finite source coordinates from zero through the last pixel center.</param>
    /// <returns>Bilinearly interpolated confidence, or an out-of-source failure.</returns>
    public Result<float, FacialError> Sample(PortraitPoint sourcePoint)
    {
        if (
            !double.IsFinite(sourcePoint.X)
            || !double.IsFinite(sourcePoint.Y)
            || sourcePoint.X < 0
            || sourcePoint.Y < 0
            || sourcePoint.X > SourceWidth - 1
            || sourcePoint.Y > SourceHeight - 1
        )
            return new FacialError(
                FacialErrorCodes.MaskSampleOutsideSource,
                "Mask sampling requires a pixel center within the matching source image."
            ).Fail<float>();
        var x = Math.Clamp((sourcePoint.X + .5) * Width / SourceWidth - .5, 0, Width - 1);
        var y = Math.Clamp((sourcePoint.Y + .5) * Height / SourceHeight - .5, 0, Height - 1);
        var left = (int)x;
        var top = (int)y;
        var right = Math.Min(left + 1, Width - 1);
        var bottom = Math.Min(top + 1, Height - 1);
        var upper =
            Confidence[top * Width + left] * (1 - (x - left))
            + Confidence[top * Width + right] * (x - left);
        var lower =
            Confidence[bottom * Width + left] * (1 - (x - left))
            + Confidence[bottom * Width + right] * (x - left);
        return ((float)(upper * (1 - (y - top)) + lower * (y - top))).Succeed();
    }
}
