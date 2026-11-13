// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using Avalonia.Threading;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Camera;
using OpenPhysical.Biometrics.Facial;

internal sealed class FaceCaptureWorkflow
{
    private readonly FaceFramePresentation display;
    private readonly FaceCameraStream camera;
    private readonly FaceCaptureProcessing processor;
    private readonly FaceCapturedPreparation preparation;
    private readonly Func<UnitResult<DesktopError>> changed;
    private readonly Func<UnitResult<DesktopError>> requestCapture;
    internal FaceAutoCapture Automatic { get; } = new();
    private int revision;
    private bool active;
    private bool stopped;
    internal FaceCameraSelection Selection { get; }
    internal FaceCaptureDraft Draft { get; } = new();
    internal FaceFramePresentation Display => display;
    internal bool Busy { get; private set; }
    internal bool IsLive => display.Publication.IsLive;
    internal Maybe<PortraitAnalysis> LiveAnalysis => preparation.Analysis;
    internal string Guidance { get; private set; } = "Choose a camera and position your face.";
    internal Maybe<DesktopError> Problem { get; private set; }

    internal FaceCaptureWorkflow(
        ICameraProvider provider,
        PortraitPreparer portraits,
        IFacialRecordPreparer records,
        Func<UnitResult<DesktopError>> changed,
        Func<UnitResult<DesktopError>> requestCapture
    )
    {
        this.changed = changed;
        this.requestCapture = requestCapture;
        display = new(changed);
        processor = new(portraits, records);
        preparation = new(processor);
        Selection = new(provider);
        camera = new(provider, display.Present, AnalyzeLiveAsync, SourceFailed);
    }

    internal async Task<UnitResult<DesktopError>> ActivateAsync(bool value)
    {
        if (stopped)
            return UnitResult.Success<DesktopError>();
        var expected = revision;
        active = value;
        if (!value)
        {
            Automatic.Reset();
            display.Publication.Pause();
            return await camera.StopAsync();
        }
        if (Draft.Source.HasValue || IsLive)
            return UnitResult.Success<DesktopError>();
        if (Selection.Sources.IsEmpty)
        {
            var refreshed = await Selection.RefreshAsync();
            if (refreshed.IsFailure)
                return Fail(refreshed.Error);
        }
        if (Selection.Source.Id.Length == 0)
            return Fail(new("Face.Camera", "Connect a camera and check camera permissions."));
        if (stopped || !active || expected != revision)
            return UnitResult.Success<DesktopError>();
        Automatic.Reset();
        display.Publication.Begin();
        preparation.Clear();
        Guide("Connecting camera…");
        var start = await camera.StartAsync(Selection.Source, Selection.Mode);
        if (stopped || !active || expected != revision)
            return UnitResult.Success<DesktopError>();
        if (start.IsFailure)
        {
            display.Publication.Pause();
            return Fail(start.Error);
        }
        Guidance = "Look at the camera, with your whole head visible.";
        return changed();
    }

    private async Task<UnitResult<DesktopError>> AnalyzeLiveAsync(CameraFrame frame)
    {
        var expected = revision;
        var analysis = await Task.Run(() => processor.Analyze(frame));
        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!IsLive || expected != revision || !display.IsDisplayed(frame))
                return UnitResult.Success<DesktopError>();
            if (analysis.IsFailure)
            {
                Automatic.Reset();
                return Fail(analysis.Error);
            }
            preparation.Remember(frame, analysis.Value);
            var readiness = FaceLiveGuidance.Assess(analysis.Value, processor.MouthPolicy);
            var capture = Automatic.Observe(readiness);
            Guide(
                readiness.Ready && Automatic.Enabled
                    ? "Look straight at the camera. Capturing shortly…"
                    : readiness.Guidance
            );
            return capture ? requestCapture() : UnitResult.Success<DesktopError>();
        });
    }

    internal async Task CaptureAsync()
    {
        if (Busy || !IsLive)
            return;
        var frozen = display.Publication.Freeze();
        if (frozen.IsFailure)
        {
            Fail(frozen.Error);
            return;
        }
        var expected = ++revision;
        Draft.Freeze(frozen.Value);
        Busy = true;
        changed();
        await camera.StopAsync();
        if (expected != revision)
            return;
        await PrepareCapturedAsync(frozen.Value, expected);
    }

    private async Task PrepareCapturedAsync(CameraFrame frame, int expected)
    {
        var prepared = await preparation.PrepareAsync(frame);
        if (expected != revision)
            return;
        Busy = false;
        if (prepared.IsFailure)
        {
            Fail(prepared.Error);
            return;
        }
        Draft.Begin(frame, prepared.Value.Analysis, prepared.Value.Portrait);
        Guidance = FaceCapturedPreparation.Guidance(prepared.Value.Analysis);
        if (ShowDraft().IsSuccess && !Draft.RequiresRetake)
            CaptureSound.Play();
        changed();
    }

    internal UnitResult<DesktopError> ShowDraft() => display.ShowDraft(Draft);

    private UnitResult<DesktopError> SourceFailed(CameraError error)
    {
        if (stopped)
            return UnitResult.Success<DesktopError>();
        display.Publication.Pause();
        Automatic.Reset();
        if (Draft.Source.HasNoValue)
            display.Clear();
        return Fail(new(error.Code, FaceLiveGuidance.SourceFailure(error)));
    }

    internal UnitResult<DesktopError> Fail(DesktopError error)
    {
        Problem = error;
        changed();
        return UnitResult.Failure(error);
    }

    internal UnitResult<DesktopError> Guide(string message)
    {
        Problem = default;
        Guidance = message;
        return changed();
    }

    internal async Task<UnitResult<DesktopError>> ResetAsync()
    {
        revision++;
        Automatic.Reset();
        display.Publication.Pause();
        await camera.StopAsync();
        display.Clear();
        Draft.Clear();
        preparation.Clear();
        Busy = false;
        Guide("Look at the camera, with your whole head visible.");
        return active ? await ActivateAsync(true) : UnitResult.Success<DesktopError>();
    }

    internal async Task<UnitResult<DesktopError>> StopAsync()
    {
        stopped = true;
        active = false;
        return await ResetAsync();
    }

    internal FaceReviewOperations Review() => new(this, processor, changed);

    internal UnitResult<DesktopError> SetBusy(bool value)
    {
        Busy = value;
        return changed();
    }
}
