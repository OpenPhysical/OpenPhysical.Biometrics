// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>A fenced preview presentation that acknowledges only an image actually rendered.</summary>
public sealed class PreviewUpdate
{
    private readonly CapturePreviewState state;
    private readonly CaptureFrame frame;
    private readonly long generation;

    internal PreviewUpdate(
        CapturePreviewState state,
        CaptureFrame frame,
        long generation,
        Maybe<Fs50ContactRegion> segmentation = default,
        Maybe<FingerprintPreprocessingArtifact> processing = default,
        bool ready = true,
        Maybe<Fs50WholeContactProof> proof = default,
        bool adjustingExposure = false,
        bool confidentlyEmpty = false
    )
    {
        this.state = state;
        this.frame = frame;
        this.generation = generation;
        Segmentation = segmentation;
        Processing = processing;
        Ready =
            ready
            && segmentation.Match(
                region =>
                    region.ComponentCount == 0
                    || proof.Match(
                        value => value.Qualified && MatchesSource(frame, region, value),
                        () => false
                    ),
                () => false
            );
        WholeContactProof = proof;
        AdjustingExposure = adjustingExposure;
        ConfidentlyEmpty = confidentlyEmpty && ConfidentAbsence(frame, segmentation, proof);
    }

    internal Func<bool> IsCurrent { get; init; } = () => true;

    /// <summary>Gets original-image contact evidence used for presence detection.</summary>
    public Maybe<Fs50ContactRegion> Segmentation { get; }

    /// <summary>Gets the tone-adjusted preview, without a segmentation stencil.</summary>
    public Maybe<FingerprintPreprocessingArtifact> Processing { get; }

    /// <summary>Gets whether empty-sensor learning permits capture.</summary>
    public bool Ready { get; }

    /// <summary>Gets source-bound whole-contact qualification from this exact acquisition.</summary>
    public Maybe<Fs50WholeContactProof> WholeContactProof { get; }

    /// <summary>Gets whether this exact acquisition produced an actual bounded exposure change.</summary>
    public bool AdjustingExposure { get; }

    /// <summary>Gets exact-frame confirmed absence, independently of reference readiness for capture.</summary>
    public bool ConfidentlyEmpty { get; }

    private static bool ConfidentAbsence(
        CaptureFrame frame,
        Maybe<Fs50ContactRegion> segmentation,
        Maybe<Fs50WholeContactProof> proof
    ) =>
        segmentation.Match(
            region =>
                region.Issue == Fs50ContactIssue.None
                && region.SpatialComponentCount == 0
                && proof.Match(
                    value =>
                        value.Record.Decision == Fs50WholeContactDecision.NoContact
                        && MatchesSource(frame, region, value),
                    () => false
                ),
            () => false
        );

    private static bool MatchesSource(
        CaptureFrame frame,
        Fs50ContactRegion region,
        Fs50WholeContactProof proof
    ) =>
        proof.Record.Identity.Sequence == frame.Sequence
        && proof.Record.Identity.CapturedAt == frame.CapturedAt
        && proof.Record.Identity.SourceDigest == region.Diagnostics.SourceDigest
        && proof.Record.Identity.SourceDigest == frame.PixelDigest;

    /// <summary>Gets the frame acknowledged after successful rendering.</summary>
    public Maybe<CaptureFrame> Acknowledged { get; private set; }

    /// <summary>Renders and acknowledges a current presentation, or discards a stale one.</summary>
    /// <param name="render">Renders the supplied exact frame on the presentation thread.</param>
    /// <returns>Rendering success or its typed failure.</returns>
    public UnitResult<DesktopError> Display(Func<CaptureFrame, UnitResult<DesktopError>> render) =>
        !IsCurrent()
            ? UnitResult.Success<DesktopError>()
            : state.Display(
                frame,
                generation,
                value =>
                {
                    var result = render(value);
                    if (result.IsSuccess)
                        Acknowledged = value;
                    return result;
                },
                Ready
                    && Segmentation.HasValue
                    && Segmentation.Value.Issue == Fs50ContactIssue.None
                    && Segmentation.Value.ComponentCount == 1,
                WholeContactProof
            );
}
