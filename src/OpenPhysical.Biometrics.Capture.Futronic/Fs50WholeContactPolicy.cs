// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using CSharpFunctionalExtensions;

/// <summary>Validated whole-image admission evidence with independently recorded existing tolerances.</summary>
public sealed record Fs50WholeContactPolicy
{
    /// <summary>Identifies clipped C-only windows and anchored geometry qualification.</summary>
    public const string Version = "fs50-spatial-contact-admission-v4";

    private Fs50WholeContactPolicy(
        Fs50WholeContactOptions options,
        Fs50EvidencePolicy evidence,
        Fs50EmptySensorPolicy emptySensor,
        FingerCapturePolicy presentation
    )
    {
        Options = options;
        Evidence = evidence;
        EmptySensor = emptySensor;
        Presentation = presentation;
        Digest = Fs50PolicyDigest.Create(
            Version,
            new
            {
                options,
                Evidence = evidence.Options,
                EmptySensor = emptySensor.Options,
                Presentation = presentation,
            }
        );
    }

    /// <summary>Gets validated default spatial choices and existing acquisition tolerances.</summary>
    public static Fs50WholeContactPolicy Default { get; } =
        Create(
            new(),
            Fs50EvidencePolicy.Default,
            Fs50EmptySensorPolicy.Default,
            FingerCapturePolicy.Default
        ).Value;

    /// <summary>Gets independent spatial choices.</summary>
    public Fs50WholeContactOptions Options { get; }

    /// <summary>Gets original contact coverage and reach choices.</summary>
    public Fs50EvidencePolicy Evidence { get; }

    /// <summary>Gets original reference-change and noise tolerances.</summary>
    public Fs50EmptySensorPolicy EmptySensor { get; }

    /// <summary>Gets anchored motion, overlap, removal and gap choices.</summary>
    public FingerCapturePolicy Presentation { get; }

    /// <summary>Gets version-bound SHA-256 of all choices actually used.</summary>
    public string Digest { get; }

    /// <summary>Validates spatial choices and disjoint changed/static photometric classification.</summary>
    /// <param name="options">Window, grid and independent exterior-area choices.</param>
    /// <param name="evidence">Validated original contact choices.</param>
    /// <param name="emptySensor">Validated reference choices.</param>
    /// <param name="presentation">Validated anchored presentation choices.</param>
    /// <returns>A complete policy or invalid-argument failure.</returns>
    public static Result<Fs50WholeContactPolicy, CaptureError> Create(
        Fs50WholeContactOptions options,
        Fs50EvidencePolicy evidence,
        Fs50EmptySensorPolicy emptySensor,
        FingerCapturePolicy presentation
    ) =>
        options is not null
        && evidence is not null
        && emptySensor is not null
        && presentation is not null
        && options.WindowSize is >= 8 and <= 128
        && options.GridStep >= 1
        && options.GridStep <= options.WindowSize
        && options.MinimumUncorroboratedArea is >= 1 and <= 800 * 750
        && emptySensor.Options.MinimumPresentationMeanAbsoluteDifference
            >= emptySensor.Options.MaximumStableMeanAbsoluteDifference
            ? new Fs50WholeContactPolicy(options, evidence, emptySensor, presentation)
            : new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Whole-contact spatial choices or disjoint reference tolerances are invalid."
            );
}
