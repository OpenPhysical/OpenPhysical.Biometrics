// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>Validated independent feature-contour and image-context geometry choices.</summary>
public sealed record Fs50ContactContourPolicy
{
    /// <summary>Identifies eight-connected contact with four-connected exterior hole filling.</summary>
    public const string Version = "fs50-contact-contour-v1";

    private Fs50ContactContourPolicy(Fs50ContactContourOptions options)
    {
        Options = options;
        Digest = Fs50PolicyDigest.Create(Version, options);
    }

    /// <summary>Gets the validated initial context choices.</summary>
    public static Fs50ContactContourPolicy Default { get; } = Create(new()).Value;

    /// <summary>Gets the immutable application choices.</summary>
    public Fs50ContactContourOptions Options { get; }

    /// <summary>Gets the version-bound SHA-256 of the choices.</summary>
    public string Digest { get; }

    /// <summary>Validates context choices without expanding feature admission.</summary>
    /// <param name="options">Proposed context and cut-guard choices.</param>
    /// <returns>Validated choices or invalid-argument failure.</returns>
    public static Result<Fs50ContactContourPolicy, CaptureError> Create(
        Fs50ContactContourOptions options
    ) =>
        options is not null
        && options.ContextMargin is >= 0 and <= 128
        && options.ModifiedBoundaryGuard is >= 0 and <= 32
            ? new Fs50ContactContourPolicy(options)
            : new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Contact context margin or modified-boundary guard is outside its validated range."
            );
}
