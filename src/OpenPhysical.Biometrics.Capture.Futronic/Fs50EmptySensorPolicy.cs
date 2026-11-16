// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>Validated limits for automatic empty-sensor learning and comparison.</summary>
public sealed record Fs50EmptySensorPolicy
{
    private Fs50EmptySensorPolicy(Fs50EmptySensorOptions options)
    {
        Options = options;
        Digest = Fs50PolicyDigest.Create("fs50-empty-sensor-v1", options);
    }

    /// <summary>Gets the validated default learning policy.</summary>
    public static Fs50EmptySensorPolicy Default { get; } =
        Create(new Fs50EmptySensorOptions()).Value;

    /// <summary>Gets immutable validated limits.</summary>
    public Fs50EmptySensorOptions Options { get; }

    /// <summary>Gets version-bound policy SHA-256.</summary>
    public string Digest { get; }

    /// <summary>Validates bounded finite baseline-learning choices.</summary>
    /// <param name="options">Immutable proposed settings.</param>
    /// <returns>A validated policy or invalid-argument failure.</returns>
    public static Result<Fs50EmptySensorPolicy, CaptureError> Create(
        Fs50EmptySensorOptions options
    ) =>
        options is not null
        && options.RequiredFrames is >= 2 and <= 32
        && options.MinimumSpanMilliseconds is >= 0 and <= 10000
        && options.MaximumReferences is >= 1 and <= 8
        && options.MaximumFrameGapMilliseconds is >= 1 and <= 10000
        && double.IsFinite(options.MaximumStableMeanAbsoluteDifference)
        && options.MaximumStableMeanAbsoluteDifference is >= 0 and <= 32
        && double.IsFinite(options.MinimumPresentationMeanAbsoluteDifference)
        && options.MinimumPresentationMeanAbsoluteDifference is >= 0 and <= 64
        && double.IsFinite(options.MinimumEmptyMeanGray)
        && options.MinimumEmptyMeanGray is >= 0 and <= 255
            ? new Fs50EmptySensorPolicy(options)
            : new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Empty-sensor learning settings are outside their validated bounds."
            );
}
