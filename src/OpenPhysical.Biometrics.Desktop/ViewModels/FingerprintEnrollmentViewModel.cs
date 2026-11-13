// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>The editable finger plan, acceptance state and capture actions for one person.</summary>
internal sealed class FingerprintEnrollmentViewModel : ObservableObject, IFingerprintEnrollment
{
    private readonly Func<Maybe<CapturedFingerprint>> currentCapture;
    private readonly EnrollmentCapturePresenter presentation;
    private readonly EnrollmentFingerSuggestions suggestions;
    private readonly PersonEnrollmentViewModel person;
    private FingerprintEnrollmentState state;
    private readonly EnrollmentFingerprintCommands commands;
    private bool stopping;
    private bool selecting;

    internal FingerprintEnrollmentViewModel(
        Func<Maybe<CapturedFingerprint>> currentCapture,
        Maybe<CaptureViewModel> live,
        EnrollmentFingerSuggestions suggestions,
        PersonEnrollmentViewModel person
    )
    {
        this.currentCapture = currentCapture;
        presentation = new EnrollmentCapturePresenter(live);
        this.suggestions = suggestions;
        this.person = person;
        state = FingerprintEnrollmentState.Create(suggestions);
        Adjustment = new(live, Refresh);
        commands = new EnrollmentFingerprintCommands(
            AcceptAsync,
            CanAccept,
            () => SelectAsync(ActiveFinger, true),
            () => SelectAsync(PrimaryFinger, false),
            () => SelectAsync(SecondaryFinger, false),
            AdjustAsync,
            () => !stopping && !IsBusy,
            () => TwoFingers,
            () => CanAdjustCapture
        );
        AcceptCommand.PropertyChanged += (_, _) => OnPropertyChanged(nameof(IsBusy));
        live.Tap(capture => capture.PropertyChanged += (_, _) => Refresh());
        person.PropertyChanged += (_, _) => Refresh();
        presentation.SelectFinger(ActiveFinger);
    }

    public bool TwoFingers
    {
        get => state.TwoFingers;
        set
        {
            if (value != state.TwoFingers)
                Replan(PrimaryFinger, SecondaryFinger, value);
        }
    }
    public ImmutableArray<FingerChoice> Fingers => FingerChoice.All;
    public FingerChoice PrimaryFinger
    {
        get => state.Primary;
        set
        {
            if (value is not null && value != state.Primary)
                Replan(value, SecondaryFinger, TwoFingers);
        }
    }
    public FingerChoice SecondaryFinger
    {
        get => state.Secondary;
        set
        {
            if (value is not null && value != state.Secondary)
                Replan(PrimaryFinger, value, TwoFingers);
        }
    }
    public FingerChoice ActiveFinger => state.Active;
    public bool PrimaryActive => ActiveFinger.Position == PrimaryFinger.Position;
    public bool SecondaryActive => TwoFingers && ActiveFinger.Position == SecondaryFinger.Position;
    public bool ActiveAccepted => state.ActiveAccepted;
    public ImmutableArray<CapturedFingerprint> AcceptedCaptures => state.Session.OrderedAccepted;
    public bool CanAdjustCapture => !stopping && !IsBusy && state.CanAdjust(currentCapture());
    public bool IsBusy => selecting || AcceptCommand.IsRunning || Adjustment.IsOpen;
    public string PrimaryLabel =>
        EnrollmentPresentation.Label(state.Session, PrimaryFinger.Position, UsableCapture());
    public string SecondaryLabel =>
        EnrollmentPresentation.Label(state.Session, SecondaryFinger.Position, UsableCapture());
    public string Progress =>
        $"{state.Session.Accepted.Length} of {state.Session.Plan.Length} fingerprints accepted";
    public bool Complete =>
        !Adjustment.IsOpen && state.Feedback.Length == 0 && state.Session.Complete;
    public string Instructions =>
        Adjustment.IsOpen
            ? $"{Adjustment.Title}. Apply changes or cancel to keep the pending capture."
            : EnrollmentPresentation.Instructions(
                state,
                person.Create().IsSuccess,
                UsableCapture()
            );
    public IAsyncRelayCommand AcceptCommand => commands.Accept;
    public IAsyncRelayCommand PrimarySlotCommand => commands.Primary;
    public IAsyncRelayCommand SecondarySlotCommand => commands.Secondary;
    public IAsyncRelayCommand ResetCaptureCommand => commands.Reset;
    public IAsyncRelayCommand AdjustCaptureCommand => commands.Adjust;
    public CaptureAdjustmentEditorViewModel Adjustment { get; }

    internal Result<ImmutableArray<CapturedFingerprint>, DesktopError> Selection() =>
        state.Selection(Adjustment.IsOpen);

    internal UnitResult<DesktopError> BeginStop()
    {
        stopping = true;
        return commands.Notify();
    }

    internal UnitResult<DesktopError> ReleaseCaptures()
    {
        state = state with { Session = EnrollmentSession.Create(state.Session.Plan).Value };
        return UnitResult.Success<DesktopError>();
    }

    internal async Task<UnitResult<DesktopError>> ResetAsync()
    {
        if (stopping)
            return UnitResult.Success<DesktopError>();
        var discarded = await Adjustment.DiscardAsync();
        if (discarded.IsFailure)
            return discarded;
        state = FingerprintEnrollmentState.Create(suggestions);
        var reset = await presentation.ResetAsync(ActiveFinger);
        Refresh();
        return reset;
    }

    private bool CanAccept() =>
        !stopping
        && !selecting
        && !Adjustment.IsOpen
        && person.Create().IsSuccess
        && state.CanAccept(currentCapture());

    private Task AdjustAsync() =>
        CanAdjustCapture ? Adjustment.OpenAsync(UsableCapture().Value) : Task.CompletedTask;

    private Maybe<CapturedFingerprint> UsableCapture() => state.EligibleCapture(currentCapture());

    private async Task AcceptAsync()
    {
        if (!CanAccept())
            return;
        var accepted = state.Accept(currentCapture().Value);
        state = accepted.IsSuccess
            ? accepted.Value
            : state with
            {
                Feedback = accepted.Error.Message,
            };
        presentation.SelectFinger(ActiveFinger);
        Refresh();
        if (accepted.IsSuccess)
            await presentation.AdvanceAsync(!state.Session.Complete);
    }

    private async Task SelectAsync(FingerChoice finger, bool retry)
    {
        if (stopping || IsBusy)
            return;
        selecting = true;
        Refresh();
        try
        {
            state = retry ? state.Retry(currentCapture()) : state with { Active = finger };
            Refresh();
            var reviewed = retry
                ? await presentation.RetryAsync()
                : await presentation.ReviewOrResumeAsync(finger, state.AcceptedCapture(finger));
            if (reviewed.IsFailure)
                state = state with { Feedback = reviewed.Error.Message };
        }
        finally
        {
            selecting = false;
            Refresh();
        }
    }

    private UnitResult<DesktopError> Replan(FingerChoice primary, FingerChoice secondary, bool pair)
    {
        if (stopping || Adjustment.IsOpen)
            return UnitResult.Success<DesktopError>();
        var previous = ActiveFinger.Position;
        state = state.Replan(primary, secondary, pair);
        presentation.SelectFinger(ActiveFinger);
        if (previous != ActiveFinger.Position && presentation.HasActiveAttempt)
            _ = SelectAsync(ActiveFinger, false);
        return Refresh();
    }

    private UnitResult<DesktopError> Refresh()
    {
        if (stopping)
            return UnitResult.Success<DesktopError>();
        foreach (var property in EnrollmentPresentation.FingerprintProperties)
            OnPropertyChanged(property);
        return commands.Notify();
    }
}
