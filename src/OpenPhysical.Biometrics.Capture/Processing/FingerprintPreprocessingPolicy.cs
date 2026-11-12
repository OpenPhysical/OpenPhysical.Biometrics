// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using System.Globalization;
using CSharpFunctionalExtensions;

/// <summary>Validated image preparation settings, independent of native quality and extraction algorithms.</summary>
public sealed record FingerprintPreprocessingPolicy
{
    private FingerprintPreprocessingPolicy(
        bool claheEnabled,
        int tileSize,
        double clipLimit,
        double blend,
        int brightness,
        int contrast
    )
    {
        ClaheEnabled = claheEnabled;
        TileSize = tileSize;
        ClipLimit = clipLimit;
        Blend = blend;
        Brightness = brightness;
        Contrast = contrast;
        Digest = CreateDigest();
    }

    /// <summary>Gets whether bounded local histogram equalization is enabled.</summary>
    public bool ClaheEnabled { get; }

    /// <summary>Gets histogram tile width and height in source pixels.</summary>
    public int TileSize { get; }

    /// <summary>Gets the histogram clipping multiplier relative to uniform occupancy.</summary>
    public double ClipLimit { get; }

    /// <summary>Gets the blend of equalized samples, from zero through one.</summary>
    public double Blend { get; }

    /// <summary>Gets operator brightness, from minus 100 through 100.</summary>
    public int Brightness { get; }

    /// <summary>Gets operator contrast, from zero through 100.</summary>
    public int Contrast { get; }

    /// <summary>Gets the additive gray-level offset, from minus 64 through 64.</summary>
    public double BrightnessOffset => Brightness * 0.64;

    /// <summary>Gets the white-anchored gain, from one through 2.5.</summary>
    public double ContrastGain => 1 + (Contrast * 0.015);

    /// <summary>Gets whether the complete original acquisition must be preserved exactly.</summary>
    public bool IsOriginal => (!ClaheEnabled || Blend == 0) && Brightness == 0 && Contrast == 0;

    /// <summary>Gets the version-bound SHA-256 of canonical policy fields.</summary>
    public string Digest { get; }

    /// <summary>Validates bounded normalization and operator controls.</summary>
    /// <param name="claheEnabled">Enable automatic local contrast normalization.</param>
    /// <param name="tileSize">Tile size from eight through 128 pixels.</param>
    /// <param name="clipLimit">Finite uniform-occupancy multiplier from one through eight.</param>
    /// <param name="blend">Finite equalized-image blend from zero through one.</param>
    /// <param name="brightness">Operator brightness from minus 100 through 100.</param>
    /// <param name="contrast">Operator contrast from zero through 100.</param>
    /// <returns>A validated immutable policy or invalid-argument failure.</returns>
    public static Result<FingerprintPreprocessingPolicy, CaptureError> Create(
        bool claheEnabled = true,
        int tileSize = 32,
        double clipLimit = 2,
        double blend = 0.5,
        int brightness = 0,
        int contrast = 20
    ) =>
        tileSize is >= 8 and <= 128
        && double.IsFinite(clipLimit)
        && clipLimit is >= 1 and <= 8
        && double.IsFinite(blend)
        && blend is >= 0 and <= 1
        && brightness is >= -100 and <= 100
        && contrast is >= 0 and <= 100
            ? new FingerprintPreprocessingPolicy(
                claheEnabled,
                tileSize,
                clipLimit,
                blend,
                brightness,
                contrast
            )
            : new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Fingerprint preprocessing settings are outside their validated bounds."
            );

    private string CreateDigest() =>
        CaptureProcessingDigest.Fields(
            FingerprintPreprocessing.Version,
            ClaheEnabled.ToString(),
            TileSize.ToString(CultureInfo.InvariantCulture),
            ClipLimit.ToString("R", CultureInfo.InvariantCulture),
            Blend.ToString("R", CultureInfo.InvariantCulture),
            Brightness.ToString(CultureInfo.InvariantCulture),
            Contrast.ToString(CultureInfo.InvariantCulture)
        );
}
