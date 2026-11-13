// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CommunityToolkit.Mvvm.Input;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

/// <summary>Applies and persists desktop configuration through application commands.</summary>
public sealed class SettingsViewModel : SettingsControlsViewModel
{
    private readonly SettingsRecovery recovery;
    private readonly Func<DesktopPreferences, Task<UnitResult<DesktopError>>> apply;
    private int version;
    private DesktopPreferences requested = DesktopPreferences.Default;
    private Task<UnitResult<DesktopError>> applying = Task.FromResult(
        UnitResult.Success<DesktopError>()
    );

    /// <summary>Creates settings commands with an explicit application configuration boundary.</summary>
    /// <param name="storage">Private configuration storage.</param>
    /// <param name="apply">Applies validated configuration and reports application failures.</param>
    /// <param name="logger">Detailed settings diagnostics, separate from operator notices.</param>
    public SettingsViewModel(
        SettingsStorage storage,
        Func<DesktopPreferences, Task<UnitResult<DesktopError>>> apply,
        ILogger<SettingsViewModel> logger
    )
    {
        recovery = new(storage, logger);
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

    /// <summary>Gets the command that persists current valid choices.</summary>
    public IAsyncRelayCommand SaveCommand { get; }

    /// <summary>Gets the command that restores and saves initial choices.</summary>
    public IAsyncRelayCommand DefaultsCommand { get; }

    /// <summary>Loads and applies current configuration.</summary>
    /// <returns>Validated data or a precise application failure.</returns>
    public async Task<UnitResult<DesktopError>> InitializeAsync()
    {
        var loaded = await recovery.LoadAsync(CancellationToken.None);
        if (Stopping)
            return UnitResult.Success<DesktopError>();
        SetValues(loaded.Preferences);
        applying = ApplyAsync(
            loaded.Preferences,
            version,
            Result.Success<DesktopPreferences, DesktopError>(loaded.Preferences)
        );
        var applied = await applying;
        if (!Stopping && applied.IsSuccess)
        {
            Notice = loaded.Notice;
            if (loaded.PersistenceFailure.HasValue)
                Status = SettingsRecovery.ResetFailure;
        }
        return applied.IsFailure
            ? applied
            : loaded.PersistenceFailure.Match(
                error => UnitResult.Failure(error),
                () => UnitResult.Success<DesktopError>()
            );
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
        var drained = await DesktopCommandTasks.DrainAsync(SaveCommand, DefaultsCommand);
        var applied = await SettleApplicationAsync();
        return drained.IsFailure ? drained : applied;
    }

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
            requested,
            Threshold,
            AutoCaptureDelaySeconds,
            MouthTolerancePercent,
            AutoCapture,
            Invert
        );
        edited = WithPolicies(edited);
        requested = edited;
        applying = ApplyAsync(edited, version, current);
        return current.IsFailure
            ? UnitResult.Failure(current.Error)
            : UnitResult.Success<DesktopError>();
    }

    private async Task<UnitResult<DesktopError>> ApplyAsync(
        DesktopPreferences preferences,
        int expected,
        Result<DesktopPreferences, DesktopError> current
    )
    {
        var attempt = await DesktopEdge.TryAsync(
            () => apply(preferences),
            "Settings.Apply",
            "The settings could not be applied."
        );
        var applied = attempt.IsSuccess ? attempt.Value : UnitResult.Failure(attempt.Error);
        if (applied.IsFailure)
            recovery.RecordFailure("apply", applied.Error);
        if (Stopping || expected != version)
            return applied;
        Status =
            applied.IsFailure ? SettingsRecovery.ApplyFailure
            : current.IsFailure ? current.Error.Message
            : "Changes apply immediately. Save to keep them for the next session.";
        if (applied.IsFailure)
            Notice = new SettingsNotice(SettingsRecovery.ApplyFailure, true);
        return applied;
    }

    private async Task<UnitResult<DesktopError>> SettleApplicationAsync()
    {
        while (true)
        {
            var current = applying;
            var applied = await current;
            if (ReferenceEquals(current, applying))
                return applied;
        }
    }

    private async Task SaveAsync(CancellationToken token)
    {
        if (Stopping)
            return;
        var applied = await SettleApplicationAsync();
        if (Stopping)
            return;
        if (applied.IsFailure)
        {
            Status = SettingsRecovery.ApplyFailure;
            return;
        }
        var current = Preferences();
        if (current.IsFailure)
        {
            Status = current.Error.Message;
            return;
        }
        var expected = version;
        var saved = await recovery.SaveAsync(current.Value, token);
        if (!Stopping)
        {
            Status =
                saved.IsFailure ? SettingsRecovery.SaveFailure
                : expected == version ? "Settings saved."
                : "Previous settings saved. Save the current changes to keep them.";
            if (saved.IsFailure && saved.Error.Code != "Cancelled")
                Notice = new SettingsNotice(SettingsRecovery.SaveFailure, true);
        }
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
        requested = preferences;
        RestoreControls(preferences);
        ImageAdjustments.Restore(preferences.ImageAdjustments);
        CapturePolicy.Restore(preferences.CapturePolicy);
        return UnitResult.Success<DesktopError>();
    }
}
