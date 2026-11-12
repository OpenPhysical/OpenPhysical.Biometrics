// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

/// <summary>Validated geometry-preserving image adjustment choices.</summary>
public sealed record ImageAdjustmentSettings
{
    private ImageAdjustmentSettings(bool automaticEnhancement, int brightness, int contrast)
    {
        AutomaticEnhancement = automaticEnhancement;
        Brightness = brightness;
        Contrast = contrast;
    }

    /// <summary>Gets initial application image controls.</summary>
    public static ImageAdjustmentSettings Default { get; } = new(false, 0, 20);

    /// <summary>Gets neutral controls that preserve source pixels.</summary>
    public static ImageAdjustmentSettings Original { get; } = new(false, 0, 0);

    /// <summary>Gets whether local enhancement is enabled.</summary>
    public bool AutomaticEnhancement { get; }

    /// <summary>Gets the signed brightness control.</summary>
    public int Brightness { get; }

    /// <summary>Gets the white-anchored contrast control.</summary>
    public int Contrast { get; }

    /// <summary>Validates image controls.</summary>
    /// <param name="automaticEnhancement">Whether local enhancement is enabled.</param>
    /// <param name="brightness">Signed brightness control.</param>
    /// <param name="contrast">White-anchored contrast control.</param>
    /// <returns>Validated data or a precise application failure.</returns>
    public static Result<ImageAdjustmentSettings, DesktopError> Create(
        bool automaticEnhancement,
        int brightness,
        int contrast
    ) =>
        brightness is >= -100 and <= 100 && contrast is >= 0 and <= 100
            ? new ImageAdjustmentSettings(automaticEnhancement, brightness, contrast)
            : new DesktopError(
                "Settings.ImageAdjustment",
                "Brightness must be −100 to 100 and contrast 0 to 100."
            );
}
