// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

/// <summary>The facial workspace actions, shared by its view and person-session lifetime.</summary>
public sealed class FaceCaptureCommands
{
    internal FaceCaptureCommands(
        FaceCaptureWorkflow workflow,
        FaceReviewOperations review,
        Func<Task> refresh,
        Func<Task<UnitResult<DesktopError>>> reset,
        Func<bool> canAccept,
        Func<bool> isBusy
    )
    {
        CameraRefresh = new AsyncRelayCommand(
            refresh,
            () => !workflow.Busy && workflow.Draft.Source.HasNoValue
        );
        Func<bool> ready = () => CaptureReady(workflow, isBusy);
        Capture = new AsyncRelayCommand(
            () => ready() ? workflow.CaptureAsync() : Task.CompletedTask,
            ready
        );
        Accept = new AsyncRelayCommand(
            () => canAccept() ? review.AcceptAsync() : Task.CompletedTask,
            canAccept
        );
        Retake = new AsyncRelayCommand(() => reset(), () => !workflow.Busy);
        Override = new AsyncRelayCommand(review.OverrideAsync, () => review.CanOverride);
    }

    private static bool CaptureReady(FaceCaptureWorkflow workflow, Func<bool> isBusy) =>
        !isBusy() && workflow.IsLive && workflow.Display.Publication.Displayed.HasValue;

    internal UnitResult<DesktopError> RequestAutomaticCapture()
    {
        if (Capture.CanExecute(default))
            Capture.Execute(default);
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Gets camera discovery.</summary>
    public IAsyncRelayCommand CameraRefresh { get; }

    /// <summary>Gets exact displayed-frame capture.</summary>
    public IAsyncRelayCommand Capture { get; }

    /// <summary>Gets explicit facial acceptance and single encoding.</summary>
    public IAsyncRelayCommand Accept { get; }

    /// <summary>Gets a fresh camera presentation.</summary>
    public IAsyncRelayCommand Retake { get; }

    /// <summary>Records an explicit exception before ordinary review, without accepting the capture.</summary>
    public IAsyncRelayCommand Override { get; }

    /// <summary>Gets all commands drained by the person session.</summary>
    public ImmutableArray<IAsyncRelayCommand> All =>
        [CameraRefresh, Capture, Accept, Retake, Override];
}
