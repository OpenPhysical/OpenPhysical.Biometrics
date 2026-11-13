// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

internal sealed class SettingsRecovery(SettingsStorage storage, ILogger logger)
{
    internal const string SaveFailure =
        "Unable to save settings. Your changes apply to this session.";
    internal const string ResetFailure = "Defaults are active, but could not be saved.";
    internal const string ApplyFailure = "The settings could not be applied. Please try again.";

    internal UnitResult<DesktopError> RecordFailure(string operation, DesktopError error)
    {
        logger.LogError(
            "Settings {Operation} failed ({Code}): {Message}",
            operation,
            error.Code,
            error.Message
        );
        return UnitResult.Success<DesktopError>();
    }

    internal async Task<SettingsLoadOutcome> LoadAsync(CancellationToken token)
    {
        var loaded = await storage.LoadAsync(token);
        if (loaded.IsSuccess)
            return new(loaded.Value, default, default);
        logger.LogWarning(
            "Settings load failed ({Code}): {Message}",
            loaded.Error.Code,
            loaded.Error.Message
        );
        if (loaded.Error.Code == "Cancelled")
            return new(DesktopPreferences.Default, default, loaded.Error);
        var saved = await SaveAsync(DesktopPreferences.Default, token);
        return new(
            DesktopPreferences.Default,
            new SettingsNotice(
                saved.IsSuccess
                    ? "Unable to load settings, resetting."
                    : "Unable to load settings. " + ResetFailure,
                saved.IsFailure
            ),
            saved.IsFailure ? saved.Error : Maybe<DesktopError>.None
        );
    }

    internal async Task<UnitResult<DesktopError>> SaveAsync(
        DesktopPreferences preferences,
        CancellationToken token
    )
    {
        var saved = await storage.SaveAsync(preferences, token);
        if (saved.IsFailure)
            RecordFailure("save", saved.Error);
        return saved;
    }
}
