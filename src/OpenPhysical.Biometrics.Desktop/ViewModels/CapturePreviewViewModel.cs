// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Exposes acknowledged preview state and capture availability.</summary>
/// <param name="clock">Monotonic presentation clock.</param>
public abstract class CapturePreviewViewModel(TimeProvider clock) : CaptureDisplayViewModel
{
    private protected readonly CaptureAutomation automation = new(clock);
    private protected abstract bool IsStopping { get; }

    /// <summary>Gets the command that freezes the acknowledged image and prepares its template.</summary>
    public abstract IAsyncRelayCommand CaptureCommand { get; }
    private protected bool HasUsablePreview =>
        Scene.HasValue
        && LiveFrame.Match(value => value.Ready, () => false)
        && LiveFrame
            .Bind(value => value.WholeContactProof)
            .Match(value => value.Qualified, () => false)
        && LiveFrame
            .Bind(value => value.Segmentation)
            .Match(
                region =>
                    region.Issue == Fs50ContactIssue.None
                    && region.ComponentCount == 1
                    && region.SpatialComponentCount == 1,
                () => false
            );

    internal UnitResult<DesktopError> Display(
        CaptureFrame frame,
        Maybe<Fs50ContactRegion> foreground = default,
        Maybe<FingerprintPreprocessingArtifact> processing = default,
        bool ready = true,
        Maybe<Fs50WholeContactProof> proof = default
    )
    {
        if (IsStopping)
            return UnitResult.Success<DesktopError>();
        var present = foreground.HasValue && foreground.Value.Diagnostics.CandidateArea > 0;
        if (present)
            LiveFrame = new DisplayedFingerprintFrame(frame, foreground)
            {
                Processing = processing,
                Ready = ready,
                WholeContactProof = proof,
            };
        var result = present
            ? Present(DisplayedSceneFactory.Create(LiveFrame.Value))
            : ClearPreview(clearDetails: false);
        return result.IsFailure ? result : DescribePreview(frame, foreground);
    }

    private UnitResult<DesktopError> DescribePreview(
        CaptureFrame frame,
        Maybe<Fs50ContactRegion> foreground
    )
    {
        var details = ProcessingDetails.UpdateSource(
            frame,
            Maybe<FingerprintPreprocessingArtifact>.None,
            foreground.Map(region => region.Diagnostics)
        );
        if (details.IsFailure)
            return details;
        Problem = string.Empty;
        ScannerResetRequired = false;
        Status =
            $"Live · {frame.Width} × {frame.Height} · 500 ppi · exposure {frame.Provenance.Settings.Exposure}";
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> RefreshCommands()
    {
        CaptureCommand.NotifyCanExecuteChanged();
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> AcknowledgeDisplayed(
        CaptureFrame frame,
        Maybe<Fs50ContactRegion> foreground,
        bool ready = true,
        Maybe<Fs50WholeContactProof> proof = default,
        bool adjustingExposure = false,
        bool confidentlyEmpty = false
    )
    {
        if (IsStopping)
            return UnitResult.Success<DesktopError>();
        var feedback = CapturePreviewGuidance.Feedback(foreground, proof);
        if (!ready)
            return WaitForCaptureReady(
                frame,
                foreground,
                feedback,
                adjustingExposure,
                confidentlyEmpty
            );
        var observed = feedback.HasValue
            ? automation.RejectContact()
            : automation.Observe(
                frame,
                foreground.Match(value => value.ContactMask, () => ImmutableArray<byte>.Empty),
                foreground.Match(value => value.ComponentCount, () => 0),
                AutoCapture
            );
        return FinishObservation(observed, feedback);
    }

    private UnitResult<DesktopError> FinishObservation(
        Result<CaptureAutomationObservation, DesktopError> observed,
        Maybe<DesktopError> feedback
    )
    {
        if (observed.IsFailure)
            return PresentError(observed.Error);
        CaptureGuidance = feedback.Match(error => error.Message, () => observed.Value.Guidance);
        if (feedback.HasValue)
            Status = $"{feedback.Value.Message} ({feedback.Value.Code})";
        if (
            feedback.HasNoValue
            && observed.Value.ShouldCapture
            && CaptureCommand.CanExecute(default)
        )
            _ = CaptureCommand.ExecuteAsync(default);
        else
            automation.TakeSelection();
        return UnitResult.Success<DesktopError>();
    }

    private UnitResult<DesktopError> ShowExposureAdjustment()
    {
        automation.RestartTiming();
        CaptureGuidance = "Adjusting the scanner. Keep your finger here.";
        Status = $"{CaptureGuidance} (Capture.AdjustingExposure)";
        return UnitResult.Success<DesktopError>();
    }

    private UnitResult<DesktopError> WaitForCaptureReady(
        CaptureFrame frame,
        Maybe<Fs50ContactRegion> foreground,
        Maybe<DesktopError> feedback,
        bool adjustingExposure,
        bool confidentlyEmpty
    )
    {
        if (confidentlyEmpty)
        {
            var observed = automation.Observe(frame, [], 0, AutoCapture);
            return observed.IsFailure
                ? PresentError(observed.Error)
                : WaitForSensor(foreground, feedback, restartTiming: false);
        }
        return adjustingExposure ? ShowExposureAdjustment() : WaitForSensor(foreground, feedback);
    }

    private UnitResult<DesktopError> WaitForSensor(
        Maybe<Fs50ContactRegion> foreground,
        Maybe<DesktopError> feedback,
        bool restartTiming = true
    )
    {
        if (restartTiming)
            automation.RestartTiming();
        CaptureGuidance = CapturePreviewGuidance.WaitingForSensor(foreground, feedback);
        if (feedback.HasValue)
            Status = $"{feedback.Value.Message} ({feedback.Value.Code})";
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Presents a typed operation failure unless shutdown has begun.</summary>
    /// <param name="error">Operator-safe error.</param>
    /// <returns>Presentation outcome.</returns>
    public UnitResult<DesktopError> Report(DesktopError error)
    {
        if (IsStopping)
            return UnitResult.Success<DesktopError>();
        CaptureCommand.NotifyCanExecuteChanged();
        return PresentError(error);
    }

    /// <inheritdoc/>
    private protected override UnitResult<DesktopError> CaptureModeChanged()
    {
        CaptureGuidance =
            Captured.HasValue ? "Captured"
            : AutoCapture ? "Place your finger"
            : "Manual capture";
        return automation.RestartTiming();
    }

    internal UnitResult<DesktopError> ScannerReady()
    {
        automation.Reset(false);
        Problem = string.Empty;
        ScannerResetRequired = false;
        CaptureGuidance = Captured.HasValue ? "Captured" : "Place your finger";
        return UnitResult.Success<DesktopError>();
    }
}
