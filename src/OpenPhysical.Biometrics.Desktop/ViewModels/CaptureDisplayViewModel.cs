// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Exposes the displayed fingerprint, acquisition settings and operator feedback.</summary>
public abstract class CaptureDisplayViewModel
    : CaptureAcquisitionControlsViewModel,
        ICapturedFingerprintSource
{
    private protected CaptureDisplayViewModel()
    {
        PropertyChanged += (_, change) =>
        {
            if (
                change.PropertyName
                is nameof(Scene)
                    or nameof(Captured)
                    or nameof(CaptureGuidance)
                    or nameof(Problem)
            )
            {
                OnPropertyChanged(nameof(HasProblem));
                OnPropertyChanged(nameof(AcquisitionMessage));
                OnPropertyChanged(nameof(HasAcquisitionMessage));
            }
        };
    }

    private Maybe<DisplayedFingerprintFrame> liveFrame;
    private Maybe<FingerprintScene> scene;
    private Maybe<CapturedFingerprint> captured;
    private string status = "Looking for an FS50…";
    private string guidance = "Place your finger";
    private bool automaticCapture;
    private bool overlay = true;
    private bool segmentation = true;
    private string problem = string.Empty;
    private bool scannerReset;

    /// <summary>Gets the diagnostic stage viewer for the current observation.</summary>
    public CaptureProcessingDetailsViewModel ProcessingDetails { get; } = new();

    /// <summary>Gets whether the scanner requires an explicit reconnect.</summary>
    public bool ScannerResetRequired
    {
        get => scannerReset;
        private protected set => SetProperty(ref scannerReset, value);
    }

    /// <summary>Gets the current actionable acquisition or processing failure.</summary>
    public string Problem
    {
        get => problem;
        private protected set => SetProperty(ref problem, value);
    }

    /// <summary>Gets whether an actionable failure is present.</summary>
    public bool HasProblem => Problem.Length > 0;

    /// <summary>Gets the instruction associated with the displayed image.</summary>
    public string AcquisitionMessage =>
        Scene.HasValue || Captured.HasValue
            ? HasProblem
                ? Problem
                : Captured.HasNoValue
                    ? CaptureGuidance
                    : string.Empty
            : string.Empty;

    /// <summary>Gets whether the displayed image has an instruction.</summary>
    public bool HasAcquisitionMessage => AcquisitionMessage.Length > 0;

    /// <summary>Gets the exact displayed acquisition and processing association.</summary>
    public Maybe<DisplayedFingerprintFrame> LiveFrame
    {
        get => liveFrame;
        private protected set => SetProperty(ref liveFrame, value);
    }

    /// <summary>Gets the current owned visualization.</summary>
    public Maybe<FingerprintScene> Scene
    {
        get => scene;
        private protected set => SetProperty(ref scene, value);
    }

    /// <summary>Gets the completed immutable capture.</summary>
    public Maybe<CapturedFingerprint> Captured
    {
        get => captured;
        private protected set
        {
            if (SetProperty(ref captured, value))
            {
                value.Match(ProcessingDetails.UpdateCapture, ProcessingDetails.Clear);
                OnPropertyChanged(nameof(Diagnostics));
            }
        }
    }

    /// <summary>Gets or sets whether valid presentations trigger capture automatically.</summary>
    public bool AutoCapture
    {
        get => automaticCapture;
        set
        {
            if (SetProperty(ref automaticCapture, value))
                CaptureModeChanged();
        }
    }

    /// <summary>Gets measured feature and quality summaries.</summary>
    public CaptureDiagnostics Diagnostics => CaptureDiagnostics.For(Captured);

    /// <summary>Gets the current capture instruction.</summary>
    public string CaptureGuidance
    {
        get => guidance;
        private protected set => SetProperty(ref guidance, value);
    }

    /// <summary>Gets the current acquisition or processing status.</summary>
    public string Status
    {
        get => status;
        private protected set => SetProperty(ref status, value);
    }

    /// <summary>Gets or sets whether encoded minutiae are visible.</summary>
    public bool ShowOverlay
    {
        get => overlay;
        set => SetProperty(ref overlay, value);
    }

    /// <summary>Gets or sets whether final area evidence is visible.</summary>
    public bool ShowSegmentation
    {
        get => segmentation;
        set => SetProperty(ref segmentation, value);
    }

    private protected UnitResult<DesktopError> ClearPreview(bool clearDetails = true)
    {
        LiveFrame = Maybe<DisplayedFingerprintFrame>.None;
        Scene = SceneLifetime.Clear(Scene);
        if (clearDetails)
            ProcessingDetails.Clear();
        return UnitResult.Success<DesktopError>();
    }

    private protected UnitResult<DesktopError> PresentError(DesktopError error)
    {
        Status = $"{error.Message} ({error.Code})";
        Problem = error.Message;
        CaptureGuidance = error.Message;
        return UnitResult.Success<DesktopError>();
    }

    private protected abstract UnitResult<DesktopError> CaptureModeChanged();

    private protected UnitResult<DesktopError> Present(
        Result<FingerprintScene, DesktopError> result
    )
    {
        if (result.IsFailure)
            return UnitResult.Failure(result.Error);
        Scene = SceneLifetime.Replace(Scene, result.Value);
        return UnitResult.Success<DesktopError>();
    }

    private protected UnitResult<DesktopError> ShowFrozen(
        CaptureFrame frame,
        Maybe<FingerprintPreprocessingArtifact> displayed
    )
    {
        if (displayed.HasValue && !ReferenceEquals(frame, displayed.Value.OriginalFrame))
            return UnitResult.Failure(
                new DesktopError(
                    "Capture.FrozenSourceMismatch",
                    "The frozen preview does not belong to the selected capture."
                )
            );
        Status = "Preparing the frozen capture…";
        LiveFrame = new DisplayedFingerprintFrame(frame, default) { Processing = displayed };
        ProcessingDetails.UpdateSource(frame, default, default);
        return Present(DisplayedSceneFactory.Frozen(LiveFrame.Value));
    }

    private protected UnitResult<DesktopError> CompleteCapture(
        Result<CapturedFingerprint, DesktopError> result
    )
    {
        if (result.IsFailure)
            return PresentError(result.Error);
        Captured = result.Value;
        var shown = Present(SceneFactory.Captured(result.Value));
        if (shown.IsFailure)
            return PresentError(shown.Error);
        Problem = string.Empty;
        CaptureGuidance = "Captured";
        Status = CaptureDiagnostics.Describe(result.Value);
        return UnitResult.Success<DesktopError>();
    }
}
