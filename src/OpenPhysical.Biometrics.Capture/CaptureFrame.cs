// SPDX-License-Identifier: GPL-2.0-or-later

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Capture;

/// <summary>An immutable, tightly packed Gray8 image with explicit sensor/correction stage.</summary>
public sealed class CaptureFrame
{
    private readonly Lazy<string> pixelDigest;

    private CaptureFrame(
        ImmutableArray<byte> pixels,
        long sequence,
        DateTimeOffset capturedAt,
        CaptureProvenance provenance
    )
    {
        Pixels = pixels;
        Sequence = sequence;
        CapturedAt = capturedAt;
        Provenance = provenance;
        pixelDigest = new Lazy<string>(
            () => CaptureProcessingDigest.Pixels(Pixels.AsSpan()),
            LazyThreadSafetyMode.ExecutionAndPublication
        );
    }

    /// <summary>Acquisition stage of the image.</summary>
    public enum FrameStage
    {
        /// <summary>Uncorrected sensor samples.</summary>
        RawSensor,

        /// <summary>Samples corrected using recorded calibration.</summary>
        Corrected,
    }

    /// <summary>Gets an owned immutable pixel copy, zero black and 255 white.</summary>
    public ImmutableArray<byte> Pixels { get; }

    /// <summary>Gets the lowercase SHA-256 of the owned Gray8 samples, shared across image roles.</summary>
    public string PixelDigest => pixelDigest.Value;

    /// <summary>Gets the pixel encoding.</summary>
    public string PixelFormat => "Gray8";

    /// <summary>Gets the width.</summary>
    public int Width => Provenance.OutputGeometry.Width;

    /// <summary>Gets the height.</summary>
    public int Height => Provenance.OutputGeometry.Height;

    /// <summary>Gets the stage derived from validated provenance.</summary>
    public FrameStage Stage =>
        Provenance.Settings.Corrected ? FrameStage.Corrected : FrameStage.RawSensor;

    /// <summary>Gets the monotonic session sequence, beginning at zero.</summary>
    public long Sequence { get; }

    /// <summary>Gets the acquisition instant normalized to UTC.</summary>
    public DateTimeOffset CapturedAt { get; }

    /// <summary>Gets acquisition evidence.</summary>
    public CaptureProvenance Provenance { get; }

    /// <summary>Validates then copies image bytes.</summary>
    /// <param name="pixels">Tightly packed Gray8 samples.</param>
    /// <param name="sequence">Nonnegative session sequence.</param>
    /// <param name="capturedAt">Acquisition time.</param>
    /// <param name="provenance">Validated evidence.</param>
    /// <returns>A frame or precise metadata/transfer failure.</returns>
    public static Result<CaptureFrame, CaptureError> Create(
        ReadOnlySpan<byte> pixels,
        long sequence,
        DateTimeOffset capturedAt,
        CaptureProvenance provenance
    )
    {
        if (provenance is null || sequence < 0 || capturedAt == default)
            return new CaptureError(
                CaptureErrorCodes.InvalidFrame,
                "Frame sequence, time or provenance is invalid."
            );
        if (pixels.Length != provenance.OutputGeometry.PixelCount)
            return new CaptureError(
                CaptureErrorCodes.TransferIncomplete,
                "The delivered byte count does not match its geometry."
            );
        return new CaptureFrame(
            ImmutableArray.Create(pixels),
            sequence,
            capturedAt.ToUniversalTime(),
            provenance
        );
    }
}
