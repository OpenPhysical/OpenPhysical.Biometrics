// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

/// <summary>Validated enhancement parameters shared by image adjustments.</summary>
public sealed record EnhancementSettings
{
    private EnhancementSettings(int tileSize, double clipLimit, double blend)
    {
        TileSize = tileSize;
        ClipLimit = clipLimit;
        Blend = blend;
    }

    /// <summary>Gets initial enhancement parameters.</summary>
    public static EnhancementSettings Default { get; } = new(32, 2, 0);

    /// <summary>Gets the local enhancement tile size.</summary>
    public int TileSize { get; }

    /// <summary>Gets the validated histogram clip limit.</summary>
    public double ClipLimit { get; }

    /// <summary>Gets the enhancement blend fraction.</summary>
    public double Blend { get; }

    /// <summary>Validates enhancement parameters.</summary>
    /// <param name="tileSize">Local tile size.</param>
    /// <param name="clipLimit">Histogram clip limit.</param>
    /// <param name="blend">Enhancement blend fraction.</param>
    /// <returns>Validated data or a precise application failure.</returns>
    public static Result<EnhancementSettings, DesktopError> Create(
        int tileSize,
        double clipLimit,
        double blend
    ) =>
        FingerprintPreprocessingPolicy
            .Create(true, tileSize, clipLimit, blend)
            .Map(_ => new EnhancementSettings(tileSize, clipLimit, blend))
            .MapError(error => new DesktopError(error.Code, error.Message));

    /// <summary>Combines these validated parameters with current image controls.</summary>
    /// <param name="image">Validated image controls.</param>
    /// <returns>Validated data or a precise application failure.</returns>
    public FingerprintPreprocessingPolicy For(ImageAdjustmentSettings image) =>
        FingerprintPreprocessingPolicy
            .Create(
                image.AutomaticEnhancement,
                TileSize,
                ClipLimit,
                Blend,
                image.Brightness,
                image.Contrast
            )
            .Value;
}
