// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;
using OpenPhysical.Biometrics.Fingerprints;

/// <summary>Validated independent acquisition, feature-contour and image-context choices.</summary>
public sealed record CapturePolicySettings
{
    private CapturePolicySettings(CapturePolicyParts parts)
    {
        Enhancement = parts.Enhancement;
        EvidenceOptions = parts.Evidence.Options;
        EmptySensorOptions = parts.EmptySensor.Options;
        MinimumNfiq1Gain = parts.Quality.MinimumNfiq1Gain;
        MinimumNfiq2Gain = parts.Quality.MinimumNfiq2Gain;
        Contour = parts.Contour;
        WholeContactOptions = parts.WholeContact.Options;
    }

    /// <summary>Gets initial application choices, independently of processing activation.</summary>
    public static CapturePolicySettings Default { get; } =
        Create(
            EnhancementSettings.Default,
            Fs50EvidencePolicy.Default.Options,
            Fs50EmptySensorPolicy.Default.Options,
            FingerprintQualitySelectionPolicy.Default.MinimumNfiq1Gain,
            FingerprintQualitySelectionPolicy.Default.MinimumNfiq2Gain,
            Fs50ContactContourPolicy.Default.Options.ContextMargin
        ).Value;

    /// <summary>Gets enhancement controls.</summary>
    public EnhancementSettings Enhancement { get; }

    /// <summary>Gets original-image contact choices.</summary>
    public Fs50EvidenceOptions EvidenceOptions { get; }

    /// <summary>Gets session-only empty-reference choices.</summary>
    public Fs50EmptySensorOptions EmptySensorOptions { get; }

    /// <summary>Gets required NFIQ1 gain for replacement.</summary>
    public int MinimumNfiq1Gain { get; }

    /// <summary>Gets required NFIQ2 gain for replacement.</summary>
    public int MinimumNfiq2Gain { get; }

    /// <summary>Gets validated timing and anchored-motion choices.</summary>
    public FingerCapturePolicy Presentation { get; private init; } = FingerCapturePolicy.Default;

    /// <summary>Gets original contact-evidence policy.</summary>
    public Fs50EvidencePolicy Evidence => Fs50EvidencePolicy.Create(EvidenceOptions).Value;

    /// <summary>Gets session reference policy.</summary>
    public Fs50EmptySensorPolicy EmptySensor =>
        Fs50EmptySensorPolicy.Create(EmptySensorOptions).Value;

    /// <summary>Gets image-quality selection policy.</summary>
    public FingerprintQualitySelectionPolicy Quality =>
        FingerprintQualitySelectionPolicy.Create(MinimumNfiq1Gain, MinimumNfiq2Gain).Value;

    /// <summary>Gets independent context margin, which never expands feature admission.</summary>
    public int ContextMargin => Contour.Options.ContextMargin;

    /// <summary>Gets contact-contour context policy with the single existing artificial-cut guard.</summary>
    public Fs50ContactContourPolicy Contour { get; }

    /// <summary>Gets spatial whole-attempt admission choices.</summary>
    public Fs50WholeContactOptions WholeContactOptions { get; private init; }

    /// <summary>Gets full original whole-contact policy, reusing existing acquisition tolerances.</summary>
    public Fs50WholeContactPolicy WholeContact =>
        Fs50WholeContactPolicy
            .Create(WholeContactOptions, Evidence, EmptySensor, Presentation)
            .Value;

    /// <summary>Validates capture choices using initial whole-contact spatial settings.</summary>
    /// <param name="enhancement">Validated enhancement settings.</param>
    /// <param name="evidence">Original contact evidence settings.</param>
    /// <param name="emptySensor">Reference learning settings.</param>
    /// <param name="nfiq1Gain">Required NFIQ1 improvement.</param>
    /// <param name="nfiq2Gain">Required NFIQ2 improvement.</param>
    /// <param name="contextMargin">Image-context margin from zero through 128 pixels.</param>
    /// <returns>Validated settings or a precise failure.</returns>
    public static Result<CapturePolicySettings, DesktopError> Create(
        EnhancementSettings enhancement,
        Fs50EvidenceOptions evidence,
        Fs50EmptySensorOptions emptySensor,
        int nfiq1Gain,
        int nfiq2Gain,
        int contextMargin
    ) =>
        CapturePolicyValidation
            .Create(enhancement, evidence, emptySensor, nfiq1Gain, nfiq2Gain, contextMargin)
            .Map(parts => new CapturePolicySettings(parts));

    /// <summary>Creates a copy with validated presentation choices.</summary>
    /// <param name="presentation">New validated presentation policy.</param>
    /// <returns>Configuration copy or policy failure.</returns>
    public Result<CapturePolicySettings, DesktopError> WithPresentation(
        FingerCapturePolicy presentation
    ) =>
        Fs50WholeContactPolicy
            .Create(WholeContactOptions, Evidence, EmptySensor, presentation)
            .MapError(CapturePolicyValidation.Error)
            .Map(_ => this with { Presentation = presentation });

    /// <summary>Creates a copy with validated spatial whole-attempt choices.</summary>
    /// <param name="options">New spatial and independent exterior-area choices.</param>
    /// <returns>Configuration copy or policy failure.</returns>
    public Result<CapturePolicySettings, DesktopError> WithWholeContact(
        Fs50WholeContactOptions options
    ) =>
        Fs50WholeContactPolicy
            .Create(options, Evidence, EmptySensor, Presentation)
            .MapError(CapturePolicyValidation.Error)
            .Map(whole => this with { WholeContactOptions = whole.Options });
}
