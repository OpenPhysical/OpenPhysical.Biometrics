// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using OpenPhysical.Workstation.Infrastructure.Security;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Shows one next step for protected pairing and current operator authority.</summary>
public sealed class StudioVerificationSetupViewModel : ObservableObject
{
    private readonly StudioVerificationSession session;
    private readonly DesktopFilePicker picker;
    private StudioVerificationState state = StudioVerificationState.Empty;
    private string status =
        "Connect this computer to check cards using your organization's signed settings.";
    private string origin = string.Empty;
    private int operatorIndex = -1;

    internal StudioVerificationSetupViewModel(
        StudioVerificationSession session,
        DesktopFilePicker picker
    )
    {
        this.session = session;
        this.picker = picker;
        PrimaryCommand = new AsyncRelayCommand(
            PrimaryAsync,
            () => !session.Busy && !ClockSetupBlocked
        );
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !session.Busy);
        SignOutCommand = new AsyncRelayCommand(
            token => ApplyAsync((backend, ct) => backend.SignOutAsync(ct), token),
            () => !session.Busy && State.SignedIn
        );
        UnpairCommand = new AsyncRelayCommand(
            token => ApplyAsync((backend, ct) => backend.UnpairAsync(ct), token),
            () => !session.Busy && State.Paired
        );
        session.PropertyChanged += (_, _) => Notify();
    }

    /// <summary>Whether connection settings can change before checking a card.</summary>
    public bool CanEdit => !session.Busy;

    /// <summary>Current authority and device choices, cleared on every failed reload.</summary>
    public StudioVerificationState State
    {
        get => state;
        private set
        {
            SetProperty(ref state, value);
            Notify();
        }
    }

    /// <summary>Plain-language next step or safe error.</summary>
    public string Status
    {
        get => status;
        private set => SetProperty(ref status, value);
    }

    /// <summary>HTTPS server address supplied with the invitation.</summary>
    public string ServerOrigin
    {
        get => origin;
        set => SetProperty(ref origin, value);
    }

    /// <summary>Selected protected operator identity index.</summary>
    public int OperatorIndex
    {
        get => operatorIndex;
        set => SetProperty(ref operatorIndex, value);
    }

    /// <summary>Protected identities available to the paired station.</summary>
    public ImmutableArray<OperatorIdentityOption> Operators => State.Operators;

    /// <summary>Whether administrator time configuration is required before checks can run.</summary>
    public bool ClockSetupBlocked =>
        State.Paired && State.SignedIn && State.ConfigurationReady && !State.ClockReady;

    /// <summary>Current setup action, without internal profile identifiers.</summary>
    public string PrimaryAction => StudioVerificationPresentation.SetupAction(State);

    /// <summary>Whether setup requires a primary action before any card check.</summary>
    public bool ShowSetupAction => !Ready;

    /// <summary>Whether initial pairing inputs are needed.</summary>
    public bool ShowPairing => !State.Paired;

    /// <summary>Whether operator sign-in is needed.</summary>
    public bool ShowOperators => State.Paired && !State.SignedIn && !Operators.IsEmpty;

    /// <summary>Whether current authority permits card-check selection.</summary>
    public bool Ready => State.Ready;

    /// <summary>Performs the single displayed setup step.</summary>
    public IAsyncRelayCommand PrimaryCommand { get; }

    /// <summary>Reads saved settings and reloads devices without starting a card check.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Clears the current local operator session.</summary>
    public IAsyncRelayCommand SignOutCommand { get; }

    /// <summary>Withdraws this computer's pairing after active operations have drained.</summary>
    public IAsyncRelayCommand UnpairCommand { get; }

    internal UnitResult<DesktopError> Cancel()
    {
        PrimaryCommand.Cancel();
        RefreshCommand.Cancel();
        SignOutCommand.Cancel();
        UnpairCommand.Cancel();
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Clear()
    {
        State = StudioVerificationState.Empty;
        ServerOrigin = string.Empty;
        OperatorIndex = -1;
        Status = "Card checking is paused.";
        return UnitResult.Success<DesktopError>();
    }

    private async Task PrimaryAsync(CancellationToken token)
    {
        if (ClockSetupBlocked)
            return;
        if (!State.Paired)
        {
            var request = await StudioVerificationFiles.PairingAsync(picker, ServerOrigin, token);
            if (request.IsFailure)
            {
                Status = request.Error.Message;
                return;
            }
            await ApplyAsync((backend, ct) => backend.PairAsync(request.Value, ct), token);
        }
        else if (!State.SignedIn)
        {
            if (Operators.IsEmpty)
            {
                await RefreshAsync(token);
                return;
            }
            if (OperatorIndex < 0 || OperatorIndex >= Operators.Length)
            {
                Status = "Choose your installed operator identity.";
                return;
            }
            var identity = Operators[OperatorIndex].Reference;
            await ApplyAsync((backend, ct) => backend.SignInAsync(identity, ct), token);
        }
        else
            await ApplyAsync((backend, ct) => backend.SynchronizeAsync(ct), token);
    }

    private async Task ApplyAsync(
        Func<IStudioVerificationBackend, CancellationToken, Task<UnitResult<DesktopError>>> action,
        CancellationToken token
    )
    {
        State = StudioVerificationState.Empty;
        Observe(await session.ApplyAndReadAsync(action, token));
    }

    private async Task RefreshAsync(CancellationToken token)
    {
        State = StudioVerificationState.Empty;
        Observe(await session.RunAsync((backend, ct) => backend.ReadAsync(ct), token));
    }

    private UnitResult<DesktopError> Observe(Result<StudioVerificationState, DesktopError> result)
    {
        if (result.IsFailure)
        {
            Status = result.Error.Message;
            return result.Error;
        }
        State = result.Value;
        OperatorIndex = Operators.Length == 1 ? 0 : -1;
        Status = StudioVerificationPresentation.SetupStatus(
            State,
            ClockSetupBlocked,
            PrimaryAction
        );
        return UnitResult.Success<DesktopError>();
    }

    private UnitResult<DesktopError> Notify()
    {
        foreach (var property in StudioVerificationPresentation.SetupProperties)
            OnPropertyChanged(property);
        foreach (
            var command in new[] { PrimaryCommand, RefreshCommand, SignOutCommand, UnpairCommand }
        )
            command.NotifyCanExecuteChanged();
        return UnitResult.Success<DesktopError>();
    }
}
