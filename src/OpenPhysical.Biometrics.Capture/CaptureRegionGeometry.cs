// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Immutable binary-region geometry without reading or comparing image brightness.</summary>
public sealed class CaptureRegionGeometry
{
    private readonly CaptureRegionMeasurements measurements;

    private CaptureRegionGeometry(CaptureGeometry geometry, ImmutableArray<byte> mask)
    {
        Geometry = geometry;
        Mask = mask;
        measurements = CaptureRegionMeasurements.Measure(geometry, mask);
    }

    /// <summary>Gets the validated source coordinate geometry.</summary>
    public CaptureGeometry Geometry { get; }

    /// <summary>Gets the owned immutable binary plane.</summary>
    public ImmutableArray<byte> Mask { get; }

    /// <summary>Gets mean included-pixel X, or zero for an empty region.</summary>
    public double CenterX => measurements.X;

    /// <summary>Gets mean included-pixel Y, or zero for an empty region.</summary>
    public double CenterY => measurements.Y;

    /// <summary>Gets the included-pixel bounding-box diagonal, or zero when empty.</summary>
    public double Diagonal => measurements.Diagonal;

    /// <summary>Gets included-pixel count.</summary>
    public int Area => measurements.Area;

    /// <summary>Validates and owns mutable input before measuring it.</summary>
    /// <param name="geometry">Source dimensions.</param>
    /// <param name="mask">Complete binary source plane.</param>
    /// <returns>Owned immutable geometry or invalid-argument failure.</returns>
    public static Result<CaptureRegionGeometry, CaptureError> Create(
        CaptureGeometry geometry,
        ReadOnlySpan<byte> mask
    )
    {
        ImmutableArray<byte> owned = [.. mask];
        return Create(geometry, owned);
    }

    /// <summary>Measures an immutable binary input without any image-pixel dependency.</summary>
    /// <param name="geometry">Source dimensions.</param>
    /// <param name="mask">Complete immutable zero/one plane.</param>
    /// <returns>Validated geometry or invalid-argument failure.</returns>
    public static Result<CaptureRegionGeometry, CaptureError> Create(
        CaptureGeometry geometry,
        ImmutableArray<byte> mask
    ) =>
        geometry is not null
        && !mask.IsDefault
        && mask.Length == (long)geometry.Width * geometry.Height
        && mask.All(value => value <= 1)
            ? new CaptureRegionGeometry(geometry, mask)
            : Invalid();

    /// <summary>Measures centroid displacement without image-pixel sampling.</summary>
    /// <param name="other">Another region in the same source geometry.</param>
    /// <returns>Centroid distance or incompatible-geometry failure.</returns>
    public Result<double, CaptureError> DisplacementFrom(CaptureRegionGeometry other) =>
        other is not null && other.Geometry == Geometry
            ? Math.Sqrt(Math.Pow(CenterX - other.CenterX, 2) + Math.Pow(CenterY - other.CenterY, 2))
            : Invalid();

    /// <summary>Measures IoU after an explicit translation of this region, without image resampling.</summary>
    /// <param name="other">Region to compare in the same geometry.</param>
    /// <param name="horizontalOffset">This region's integer X translation.</param>
    /// <param name="verticalOffset">This region's integer Y translation.</param>
    /// <returns>Included intersection over union, zero when both are empty, or invalid-input failure.</returns>
    public Result<double, CaptureError> IntersectionOverUnion(
        CaptureRegionGeometry other,
        int horizontalOffset = 0,
        int verticalOffset = 0
    )
    {
        if (
            other is null
            || other.Geometry != Geometry
            || !ValidOffset(horizontalOffset, verticalOffset)
        )
            return Invalid();
        int intersection = 0,
            union = 0;
        for (var y = 0; y < Geometry.Height; y++)
        for (var x = 0; x < Geometry.Width; x++)
        {
            var previous = At(x - horizontalOffset, y - verticalOffset);
            var current = other.Mask[y * Geometry.Width + x] != 0;
            intersection += previous && current ? 1 : 0;
            union += previous || current ? 1 : 0;
        }
        return union == 0 ? 0 : (double)intersection / union;
    }

    /// <summary>Translates only region geometry; it never borrows or changes image pixels.</summary>
    /// <param name="horizontalOffset">Integer X translation.</param>
    /// <param name="verticalOffset">Integer Y translation.</param>
    /// <returns>The clipped translated binary region or invalid-offset failure.</returns>
    public Result<ImmutableArray<byte>, CaptureError> Translate(
        int horizontalOffset,
        int verticalOffset
    )
    {
        if (!ValidOffset(horizontalOffset, verticalOffset))
            return Invalid();
        var result = new byte[Mask.Length];
        for (var y = 0; y < Geometry.Height; y++)
        for (var x = 0; x < Geometry.Width; x++)
            result[y * Geometry.Width + x] = (byte)(
                At(x - horizontalOffset, y - verticalOffset) ? 1 : 0
            );
        return ImmutableArray.CreateRange(result);
    }

    private bool At(int x, int y) =>
        x >= 0
        && y >= 0
        && x < Geometry.Width
        && y < Geometry.Height
        && Mask[y * Geometry.Width + x] != 0;

    private bool ValidOffset(int x, int y) =>
        x >= -Geometry.Width
        && x <= Geometry.Width
        && y >= -Geometry.Height
        && y <= Geometry.Height;

    private static CaptureError Invalid() =>
        new(
            CaptureErrorCodes.InvalidArgument,
            "Binary region geometry, coordinate dimensions or translation is invalid."
        );
}
