// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Workstation.Application.Verification;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>One explicit card check, including authenticated finger selection and secret cleanup.</summary>
public sealed class StudioVerificationCardViewModel : ObservableObject
{
    private readonly StudioVerificationSession session;
    private readonly StudioVerificationSetupViewModel authority;
    private readonly StudioVerificationMaterial material;
    private readonly StudioVerificationCardCommands operations;
    private Maybe<TaskCompletionSource<Result<GuidedFingerChoice, DesktopError>>> pendingChoice;
    private Maybe<CredentialVerificationResult> result;
    private string status = "Connect and sign in before checking a card.";
    private ImmutableArray<StudioFingerChoice> choices = [];
    private int fingerIndex = -1;

    internal StudioVerificationCardViewModel(
        StudioVerificationSession session,
        StudioVerificationSetupViewModel setup,
        DesktopFilePicker picker
    )
    {
        this.session = session;
        material = new StudioVerificationMaterial(picker);
        authority = setup;
        operations = new StudioVerificationCardCommands(session, this, material);
        session.PropertyChanged += (_, _) => Notify();
        setup.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(setup.State))
            {
                Input.Load(setup.State);
                ClearResult();
                Status = setup.Ready
                    ? "Insert a card and choose a check."
                    : "Connect and sign in before checking a card.";
                Notify();
            }
        };
        Input.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(Input.ChoiceIndex))
            {
                material.Clear();
                ClearResult();
                Status = "Ready for a new card check.";
            }
        };
    }

    internal bool IsReady => authority.Ready;

    /// <summary>Whether selectors can change before a check begins.</summary>
    public bool CanEdit => !session.Busy;

    /// <summary>Cancels the active check and drains its card and scanner before release.</summary>
    public IRelayCommand CancelCommand => operations.CancelCommand;

    /// <summary>Reader, check and protected input selections.</summary>
    public StudioVerificationCardInput Input { get; } = new();

    /// <summary>Plain-language progress or safe failure.</summary>
    public string Status
    {
        get => status;
        internal set => SetProperty(ref status, value);
    }

    /// <summary>Separate completed checks, cleared whenever authority or selection changes.</summary>
    public Maybe<CredentialVerificationResult> Result
    {
        get => result;
        internal set
        {
            SetProperty(ref result, value);
            OnPropertyChanged(nameof(ResultDetails));
        }
    }

    /// <summary>Individual check outcomes and stable diagnostic reasons.</summary>
    public string ResultDetails =>
        Result.Match(StudioVerificationPresentation.Details, () => string.Empty);

    /// <summary>Authenticated finger options for this exact retained card.</summary>
    public ImmutableArray<StudioFingerChoice> Choices
    {
        get => choices;
        private set
        {
            SetProperty(ref choices, value);
            OnPropertyChanged(nameof(AwaitingFinger));
        }
    }

    /// <summary>Explicit enrolled-finger selection index.</summary>
    public int FingerIndex
    {
        get => fingerIndex;
        set => SetProperty(ref fingerIndex, value);
    }

    /// <summary>Selected enrolled finger, if available.</summary>
    public Maybe<StudioFingerChoice> SelectedFinger =>
        FingerIndex >= 0 && FingerIndex < Choices.Length
            ? Choices[FingerIndex]
            : Maybe<StudioFingerChoice>.None;

    /// <summary>Whether the card has authenticated an enrolled-finger choice.</summary>
    public bool AwaitingFinger => pendingChoice.HasValue && !Choices.IsEmpty;

    /// <summary>Public candidate counts and key-presence status, never key data.</summary>
    public string MaterialSummary => material.Summary;

    /// <summary>Starts one explicit check, never an automatic PIN retry.</summary>
    public IAsyncRelayCommand PrimaryCommand => operations.PrimaryCommand;

    /// <summary>Confirms an authenticated enrolled finger before capture begins.</summary>
    public IRelayCommand ContinueFingerCommand => operations.ContinueFingerCommand;

    /// <summary>Adds one untrusted issuer candidate.</summary>
    public IAsyncRelayCommand ImportIssuerCommand => operations.ImportIssuerCommand;

    /// <summary>Adds one untrusted content-signer candidate.</summary>
    public IAsyncRelayCommand ImportSignerCommand => operations.ImportSignerCommand;

    /// <summary>Loads a separately acquired TWIC privacy key into transient owned memory.</summary>
    public IAsyncRelayCommand ImportPrivacyKeyCommand => operations.ImportPrivacyKeyCommand;

    internal UnitResult<DesktopError> Cancel()
    {
        PrimaryCommand.Cancel();
        ImportIssuerCommand.Cancel();
        ImportSignerCommand.Cancel();
        ImportPrivacyKeyCommand.Cancel();
        pendingChoice.Tap(value =>
            value.TrySetResult(new DesktopError("Cancelled", "Card check cancelled."))
        );
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Clear()
    {
        Input.Load(StudioVerificationState.Empty);
        material.Clear();
        ClearResult();
        Status = "Ready for a new card check.";
        Status = "Card checking is paused.";
        return UnitResult.Success<DesktopError>();
    }

    internal async Task<Result<GuidedFingerChoice, DesktopError>> ChooseAsync(
        ImmutableArray<GuidedFingerChoice> values,
        CancellationToken token
    )
    {
        var source = new TaskCompletionSource<Result<GuidedFingerChoice, DesktopError>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        pendingChoice = source;
        Choices = values.Select(value => new StudioFingerChoice(value)).ToImmutableArray();
        FingerIndex = Choices.Length == 1 ? 0 : -1;
        Status = "Choose one of the card's enrolled fingers, then place it on the scanner.";
        ContinueFingerCommand.NotifyCanExecuteChanged();
        using var cancelled = token.Register(() =>
            source.TrySetResult(new DesktopError("Cancelled", "Card check cancelled."))
        );
        var chosen = await source.Task;
        pendingChoice = default;
        Choices = [];
        ContinueFingerCommand.NotifyCanExecuteChanged();
        if (chosen.IsSuccess)
            Status = "Place the selected finger on the scanner.";
        return chosen;
    }

    internal UnitResult<DesktopError> Finish()
    {
        Choices = [];
        pendingChoice = default;
        return Notify();
    }

    internal UnitResult<DesktopError> SelectFinger()
    {
        if (!AwaitingFinger || SelectedFinger.HasNoValue)
            return new DesktopError("Verification.Finger", "Choose an enrolled finger.");
        pendingChoice.Value.TrySetResult(SelectedFinger.Value.Choice);
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> ClearResult()
    {
        Result = default;
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Notify()
    {
        operations.Notify();
        OnPropertyChanged(nameof(MaterialSummary));
        OnPropertyChanged(nameof(CanEdit));
        return UnitResult.Success<DesktopError>();
    }
}
