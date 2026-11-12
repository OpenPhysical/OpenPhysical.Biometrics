// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;

/// <summary>Applies and persists desktop configuration through application commands.</summary>
public sealed class SettingsViewModel : SettingsControlsViewModel
{
    private readonly SettingsStorage storage;
    private readonly Func<DesktopPreferences, UnitResult<DesktopError>> apply;
    private int version;
    private DesktopPreferences active = DesktopPreferences.Default;

    /// <summary>Creates settings commands for the active workspace.</summary>
    /// <param name="storage">Private configuration storage.</param>
    /// <param name="capture">Active capture workspace.</param>
    /// <param name="inspection">Active comparison workspace.</param>
    internal SettingsViewModel(
        SettingsStorage storage,
        CaptureViewModel capture,
        InspectionViewModel inspection
    )
        : this(storage, preferences => SettingsApplication.Apply(preferences, capture, inspection))
    { }

    /// <summary>Creates settings commands with an explicit application configuration boundary.</summary>
    /// <param name="storage">Private configuration storage.</param>
    /// <param name="apply">Applies validated configuration and reports application failures.</param>
    public SettingsViewModel(
        SettingsStorage storage,
        Func<DesktopPreferences, UnitResult<DesktopError>> apply
    )
    {
        this.storage = storage;
        this.apply = apply;
        ImageAdjustments.Changed += _ => Changed();
        CapturePolicy.Changed += _ => PolicyChanged();
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !Stopping);
        DefaultsCommand = new AsyncRelayCommand(
            DefaultsAsync,
            () => !Stopping && !SaveCommand.IsRunning
        );
        SaveCommand.PropertyChanged += (_, _) => DefaultsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Gets or sets context margin without changing feature admission.</summary>
    public int ContextMargin
    {
        get => CapturePolicy.ContextMargin;
        set => CapturePolicy.ContextMargin = value;
    }

    /// <summary>Gets the command that persists current valid choices.</summary>
    public IAsyncRelayCommand SaveCommand { get; }

    /// <summary>Gets the command that restores and saves initial choices.</summary>
    public IAsyncRelayCommand DefaultsCommand { get; }

    /// <summary>Gets image controls.</summary>
    public ImageAdjustmentViewModel ImageAdjustments { get; } = new();

    /// <summary>Gets advanced capture controls.</summary>
    public CapturePolicyViewModel CapturePolicy { get; } = new();

    /// <summary>Loads and applies current configuration.</summary>
    /// <returns>Validated data or a precise application failure.</returns>
    public async Task<UnitResult<DesktopError>> InitializeAsync()
    {
        var loaded = await storage.LoadAsync(CancellationToken.None);
        if (Stopping)
            return UnitResult.Success<DesktopError>();
        if (loaded.IsFailure)
        {
            Status = $"{loaded.Error.Message} ({loaded.Error.Code})";
            return UnitResult.Failure(loaded.Error);
        }
        SetValues(loaded.Value);
        return apply(loaded.Value);
    }

    /// <summary>Stops settings commands and drains pending persistence.</summary>
    /// <returns>Validated data or a precise application failure.</returns>
    public async Task<UnitResult<DesktopError>> StopAsync()
    {
        Stopping = true;
        SaveCommand.Cancel();
        DefaultsCommand.Cancel();
        SaveCommand.NotifyCanExecuteChanged();
        DefaultsCommand.NotifyCanExecuteChanged();
        return await DesktopCommandTasks.DrainAsync(SaveCommand, DefaultsCommand);
    }

    private Result<DesktopPreferences, DesktopError> Preferences() =>
        SettingsInput
            .Parse(Threshold, AutoCaptureDelaySeconds, AutoCapture, Invert)
            .Map(WithPolicies);

    private DesktopPreferences WithPolicies(DesktopPreferences value) =>
        value with
        {
            ImageAdjustments = ImageAdjustments.Value,
            CapturePolicy = CapturePolicy.Value,
        };

    private UnitResult<DesktopError> PolicyChanged()
    {
        OnPropertyChanged(nameof(ContextMargin));
        return Changed();
    }

    /// <inheritdoc/>
    protected override UnitResult<DesktopError> Changed()
    {
        version++;
        var current = Preferences();
        var edited = SettingsInput.Editing(
            active,
            Threshold,
            AutoCaptureDelaySeconds,
            AutoCapture,
            Invert
        );
        edited = WithPolicies(edited);
        var applied = apply(edited);
        if (applied.IsSuccess)
            active = edited;
        Status =
            applied.IsFailure ? applied.Error.Message
            : current.IsFailure ? current.Error.Message
            : "Changes apply immediately. Save to keep them for the next session.";
        return applied.IsFailure ? applied
            : current.IsFailure ? UnitResult.Failure(current.Error)
            : UnitResult.Success<DesktopError>();
    }

    private async Task SaveAsync(CancellationToken token)
    {
        if (Stopping)
            return;
        var current = Preferences();
        if (current.IsFailure)
        {
            Status = current.Error.Message;
            return;
        }
        var expected = version;
        var saved = await storage.SaveAsync(current.Value, token);
        if (!Stopping)
            Status =
                saved.IsFailure ? $"{saved.Error.Message} ({saved.Error.Code})"
                : expected == version ? "Settings saved."
                : "Previous settings saved. Save the current changes to keep them.";
    }

    private async Task DefaultsAsync(CancellationToken token)
    {
        if (Stopping)
            return;
        SetValues(DesktopPreferences.Default);
        Changed();
        await SaveAsync(token);
    }

    private UnitResult<DesktopError> SetValues(DesktopPreferences preferences)
    {
        active = preferences;
        RestoreControls(preferences);
        ImageAdjustments.Restore(preferences.ImageAdjustments);
        CapturePolicy.Restore(preferences.CapturePolicy);
        return UnitResult.Success<DesktopError>();
    }
}
