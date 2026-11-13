// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;

/// <summary>Validated desktop configuration using one current serialized schema.</summary>
public sealed record DesktopPreferences
{
    private DesktopPreferences(
        int comparisonThreshold,
        bool autoCapture,
        bool invertImage,
        double delaySeconds
    )
    {
        ComparisonThreshold = comparisonThreshold;
        AutoCapture = autoCapture;
        InvertImage = invertImage;
        AutoCaptureDelaySeconds = delaySeconds;
    }

    /// <summary>Gets initial application configuration.</summary>
    public static DesktopPreferences Default { get; } = new(40, false, false, 1);

    /// <summary>Gets the comparison decision threshold.</summary>
    public int ComparisonThreshold { get; }

    /// <summary>Gets whether automatic fingerprint capture is enabled.</summary>
    public bool AutoCapture { get; }

    /// <summary>Gets whether automatic photo capture is enabled independently of fingerprints.</summary>
    public bool FaceAutoCapture { get; init; }

    /// <summary>Gets whether captured photos replace the segmented background.</summary>
    public bool FaceBackgroundReplacement { get; init; } = true;

    /// <summary>Gets whether display pixels are inverted.</summary>
    public bool InvertImage { get; }

    /// <summary>Gets the presentation delay in seconds.</summary>
    public double AutoCaptureDelaySeconds { get; }

    /// <summary>Gets image adjustment controls.</summary>
    public ImageAdjustmentSettings ImageAdjustments { get; init; } =
        ImageAdjustmentSettings.Default;

    /// <summary>Gets advanced capture choices.</summary>
    public CapturePolicySettings CapturePolicy { get; init; } = CapturePolicySettings.Default;

    /// <summary>Gets the adjustable facial mouth-closure assessment policy.</summary>
    public PortraitMouthPolicy MouthPolicy { get; init; } = PortraitMouthPolicy.Default;

    /// <summary>Serializes this configuration using the one current settings schema.</summary>
    /// <returns>Complete UTF-8 configuration JSON.</returns>
    public byte[] ToJsonBytes() => PreferencesJson.Write(this);

    /// <summary>Reads the current schema without legacy migrations.</summary>
    /// <param name="json">Complete UTF-8 configuration JSON.</param>
    /// <returns>Validated settings or a format/settings failure.</returns>
    public static Result<DesktopPreferences, DesktopError> FromJson(ReadOnlySpan<byte> json) =>
        PreferencesJson.Read(json.ToArray());

    /// <summary>Validates primary application controls.</summary>
    /// <param name="threshold">Comparison decision threshold.</param>
    /// <param name="auto">Automatic capture setting.</param>
    /// <param name="invert">Display inversion setting.</param>
    /// <param name="delaySeconds">Presentation delay in seconds.</param>
    /// <returns>Validated data or a precise application failure.</returns>
    public static Result<DesktopPreferences, DesktopError> Create(
        int threshold,
        bool auto,
        bool invert,
        double delaySeconds
    ) =>
        CheckThreshold(threshold)
            .Bind(score =>
                CheckDelay(delaySeconds)
                    .Map(delay => new DesktopPreferences(score, auto, invert, delay))
            );

    /// <summary>Validates a comparison threshold.</summary>
    /// <param name="threshold">Requested decision threshold.</param>
    /// <returns>Validated data or a precise application failure.</returns>
    public static Result<int, DesktopError> CheckThreshold(int threshold) =>
        threshold is >= 0 and <= 10000
            ? threshold
            : new DesktopError(
                "Settings.Threshold",
                "Comparison threshold must be an integer from 0 to 10000."
            );

    /// <summary>Validates presentation delay.</summary>
    /// <param name="seconds">Requested delay in seconds.</param>
    /// <returns>Validated data or a precise application failure.</returns>
    public static Result<double, DesktopError> CheckDelay(double seconds) =>
        double.IsFinite(seconds) && seconds is >= 0 and <= 10
            ? seconds
            : new DesktopError(
                "Settings.Delay",
                "Automatic capture delay must be a finite number from 0 to 10 seconds."
            );
}
