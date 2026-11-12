// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

/// <summary>A local pending-capture draft applied before the separate Accept action.</summary>
public sealed class CaptureAdjustmentEditorViewModel : ObservableObject
{
    private readonly Maybe<CaptureViewModel> capture;
    private readonly Func<UnitResult<DesktopError>> changed;
    private readonly CaptureAdjustmentDraftSession session = new();
    private readonly CaptureAdjustmentControlBinding controls;
    private int generation;
    private bool open,
        busy,
        closing;

    internal CaptureAdjustmentEditorViewModel(
        Maybe<CaptureViewModel> capture,
        Func<UnitResult<DesktopError>> changed
    )
    {
        this.capture = capture;
        this.changed = changed;
        controls = new(PreviewAsync);
        ApplyCommand = new AsyncRelayCommand(() => FinishAsync(true), CanApply);
        CancelCommand = new AsyncRelayCommand(() => DiscardAsync(), () => IsOpen && !closing);
    }

    /// <summary>Gets local controls, independent of saved application preferences.</summary>
    public ImageAdjustmentViewModel Controls => controls.Value;

    /// <summary>Gets whether a pending-capture draft is open.</summary>
    public bool IsOpen => open;

    /// <summary>Gets whether the current preview is processing.</summary>
    public bool IsBusy => busy;

    /// <summary>Gets whether the pending capture has an editable retained original.</summary>
    public bool CanAdjust => IsOpen && !closing && session.Current.Original.Processing.HasValue;

    /// <summary>Gets the finger currently being adjusted before acceptance.</summary>
    public string Title =>
        IsOpen ? $"Editing {FingerChoice.LabelFor(session.Current.Original.Finger)}" : string.Empty;

    /// <summary>Gets changed controls from original values to draft values.</summary>
    public string ChangeSummary =>
        IsOpen ? CaptureEditSummary.For(session.Current.Initial, Controls.Value) : string.Empty;

    /// <summary>Gets the current draft's preparation failure.</summary>
    public string Error => IsOpen ? session.Current.Error : string.Empty;

    /// <summary>Gets the command returning the changed image to pending review without accepting it.</summary>
    public IAsyncRelayCommand ApplyCommand { get; }

    /// <summary>Gets the command restoring the exact pending original without accepting it.</summary>
    public IAsyncRelayCommand CancelCommand { get; }

    internal async Task OpenAsync(CapturedFingerprint source)
    {
        if (IsOpen || capture.HasNoValue)
            return;
        session.Open(source, capture.Value);
        controls.Restore(session.Current.Settings);
        open = true;
        Refresh();
        var shown = await capture.Value.ReviewAcceptedAsync(source);
        if (shown.IsFailure)
            session.Fail(shown.Error);
        Refresh();
    }

    private async Task PreviewAsync(ImageAdjustmentSettings settings)
    {
        if (!CanAdjust)
            return;
        var expected = ++generation;
        session.Begin(settings);
        busy = true;
        Refresh();
        var result = await session.PreviewAsync(capture.Value, settings);
        if (expected != generation || !IsOpen)
            return;
        session.Complete(result);
        busy = false;
        Refresh();
    }

    private bool CanApply() =>
        IsOpen && !busy && !closing && session.Current.HasChanges && session.Current.CanApply;

    internal Task<UnitResult<DesktopError>> DiscardAsync() =>
        !IsOpen ? Task.FromResult(UnitResult.Success<DesktopError>())
        : closing
            ? Task.FromResult(
                UnitResult.Failure(
                    new DesktopError(
                        "Session.EditClosing",
                        "Wait for the capture changes to finish applying."
                    )
                )
            )
        : FinishAsync(false);

    private async Task<UnitResult<DesktopError>> FinishAsync(bool commit)
    {
        if (commit && !CanApply())
            return UnitResult.Success<DesktopError>();
        generation++;
        closing = true;
        Refresh();
        var selected = commit ? session.Current.Ready.Value : session.Current.Original;
        var restored = await session.RestoreAsync(capture.Value, selected);
        if (restored.IsSuccess)
        {
            open = busy = closing = false;
            session.Clear();
        }
        else
        {
            session.Fail(restored.Error);
            closing = false;
        }
        Refresh();
        return restored;
    }

    private UnitResult<DesktopError> Refresh()
    {
        foreach (
            var property in new[]
            {
                nameof(IsOpen),
                nameof(IsBusy),
                nameof(CanAdjust),
                nameof(Title),
                nameof(ChangeSummary),
                nameof(Error),
            }
        )
            OnPropertyChanged(property);
        ApplyCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        return changed();
    }
}
