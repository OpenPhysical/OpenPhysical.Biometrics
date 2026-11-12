// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Exposes image controls for the current acquisition and frozen capture.</summary>
public abstract class CaptureAdjustedViewModel : CapturePreviewViewModel
{
    private readonly CaptureCoordinator capture;
    private readonly CaptureAdjustmentWork adjustments;
    private protected int attempt;
    private protected bool busy;
    private protected bool reviewingAccepted;
    private protected Maybe<CaptureAcquisitionPolicy> originalAcquisition;
    private protected Maybe<CaptureProcessingInput> originalInput;
    private protected bool HasCurrentProcessingPreview =>
        capture.Images.IsCurrentPreview(LiveFrame);

    private protected CaptureAdjustedViewModel(
        TimeProvider clock,
        CaptureCoordinator coordinator,
        ICaptureProcessor processor
    )
        : base(clock)
    {
        capture = coordinator;
        adjustments = new CaptureAdjustmentWork(capture.Images, processor, capture.PauseAsync);
    }

    /// <summary>Applies validated image and capture controls to this attempt.</summary>
    /// <param name="settings">Current operator image controls.</param>
    /// <param name="policy">Current validated capture policy.</param>
    /// <returns>Configuration outcome; image work publishes asynchronously.</returns>
    public UnitResult<DesktopError> ConfigureImageAdjustments(
        ImageAdjustmentSettings settings,
        CapturePolicySettings policy
    )
    {
        var change = CaptureAdjustmentConfiguration.Apply(capture, automation, settings, policy);
        if (change.Revision == change.Previous || reviewingAccepted || IsStopping)
            return UnitResult.Success<DesktopError>();
        CaptureCommand.Cancel();
        if (change.NewReadiness && LiveFrame.HasValue)
            LiveFrame = LiveFrame.Value with { Ready = false };
        var source = Captured.HasValue ? Captured : adjustments.Source;
        var frame = source.Map(value => value.Frame).Or(LiveFrame.Map(value => value.Frame));
        if (frame.HasNoValue)
            return UnitResult.Success<DesktopError>();
        var evidence = new CaptureAdjustmentInput(capture, originalAcquisition, originalInput).For(
            frame.Value,
            source,
            LiveFrame,
            policy,
            change.NewMask
        );
        if (evidence.IsFailure)
            return UnitResult.Failure(evidence.Error);
        var expected = ++attempt;
        busy = source.HasValue || !capture.IsLive;
        Captured = Maybe<CapturedFingerprint>.None;
        CaptureCommand.NotifyCanExecuteChanged();
        _ = AdjustAsync(frame.Value, evidence.Value, source, expected);
        return UnitResult.Success<DesktopError>();
    }

    private protected UnitResult<DesktopError> CancelAdjustments()
    {
        reviewingAccepted = false;
        originalAcquisition = Maybe<CaptureAcquisitionPolicy>.None;
        originalInput = Maybe<CaptureProcessingInput>.None;
        return adjustments.Cancel(true);
    }

    private protected Task<UnitResult<DesktopError>> DrainAdjustmentsAsync() =>
        adjustments.DrainAsync();

    internal CaptureEditConfiguration CreateEditConfiguration() => new(capture);

    internal Task<UnitResult<DesktopError>> RestoreEditConfigurationAsync(
        CaptureEditConfiguration configuration
    ) => configuration.RestoreAsync(DrainAdjustmentsAsync, CancelAdjustments);

    private protected UnitResult<DesktopError> PresentProcessing(
        FingerprintPreprocessingArtifact artifact
    )
    {
        LiveFrame = new DisplayedFingerprintFrame(
            artifact.OriginalFrame,
            Maybe<Fs50ContactRegion>.None
        )
        {
            Processing = artifact,
        };
        return Present(SceneFactory.Processed(artifact, []));
    }

    /// <summary>Reprocesses a retained accepted image using current controls.</summary>
    /// <param name="source">Immutable accepted capture to adjust.</param>
    /// <returns>Reprocessing outcome.</returns>
    public async Task<UnitResult<DesktopError>> AdjustAcceptedAsync(CapturedFingerprint source)
    {
        reviewingAccepted = false;
        if (source.Processing.HasNoValue)
            return new DesktopError(
                "Capture.Adjustment",
                "This capture has no retained processing image."
            );
        var evidence = CaptureAdjustmentEvidence.From(source.Processing.Value);
        var expected = ++attempt;
        busy = true;
        Captured = Maybe<CapturedFingerprint>.None;
        CaptureCommand.NotifyCanExecuteChanged();
        return await AdjustAsync(source.Frame, evidence, source, expected);
    }

    private protected UnitResult<DesktopError> ShowFrozenPreview(
        CaptureFrame frame,
        (FingerprintPreprocessingPolicy Policy, long Revision) snapshot,
        CancellationToken token
    ) =>
        capture
            .Images.PreparePreview(frame, snapshot, token)
            .Bind(image => ShowFrozen(frame, image));

    private async Task<UnitResult<DesktopError>> AdjustAsync(
        CaptureFrame frame,
        CaptureAdjustmentEvidence evidence,
        Maybe<CapturedFingerprint> source,
        int expected
    )
    {
        var acquisition = CaptureWorkSnapshot.OriginalPolicy(source, originalAcquisition);
        var result = await adjustments.RunAsync(
            frame,
            evidence,
            SelectedFinger.Position,
            capture.Policy,
            source,
            busy,
            value => ShowAdjustment(value.Artifact, value.Evidence, expected),
            capture =>
                FinishAdjustment(
                    capture.Bind(value => acquisition.Map(policy => policy.ApplyTo(value))),
                    expected
                )
        );
        if (result.IsFailure && expected == attempt && !IsStopping)
            PresentError(result.Error);
        return result;
    }

    private bool ShowAdjustment(
        FingerprintPreprocessingArtifact artifact,
        CaptureAdjustmentEvidence evidence,
        int expected
    )
    {
        if (expected != attempt || IsStopping)
            return false;
        if (
            capture.IsLive
            && LiveFrame.Match(
                value => !ReferenceEquals(value.Frame, artifact.OriginalFrame),
                () => true
            )
        )
            return false;
        LiveFrame = CaptureAdjustmentPreview.Create(artifact, evidence, LiveFrame);
        ProcessingDetails.UpdateSource(
            artifact.OriginalFrame,
            capture.IsLive ? Maybe<FingerprintPreprocessingArtifact>.None : artifact,
            evidence.Diagnostics
        );
        var shown = Present(
            capture.IsLive
                ? DisplayedSceneFactory.Create(LiveFrame.Value)
                : SceneFactory.FrozenProcessed(artifact)
        );
        CaptureCommand.NotifyCanExecuteChanged();
        return shown.IsSuccess;
    }

    private UnitResult<DesktopError> FinishAdjustment(
        Result<CapturedFingerprint, DesktopError> result,
        int expected
    )
    {
        if (expected != attempt || IsStopping)
            return UnitResult.Success<DesktopError>();
        busy = false;
        CaptureCommand.NotifyCanExecuteChanged();
        return CompleteProcessing(result);
    }

    private protected UnitResult<DesktopError> CompleteProcessing(
        Result<CapturedFingerprint, DesktopError> result
    )
    {
        if (result.IsSuccess)
            originalInput = CaptureAdjustmentInput.Selected(result.Value);
        return CompleteCapture(result);
    }
}
