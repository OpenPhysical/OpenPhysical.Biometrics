// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

/// <summary>Loads and atomically saves the current private application configuration.</summary>
/// <param name="path">Explicit private configuration file path.</param>
public sealed class SettingsStorage(string path)
{
    /// <summary>Gets the private application configuration path.</summary>
    public static string DefaultPath =>
        Path.Combine(DesktopPaths.UserData, "Biometrics", "preferences.json");

    /// <summary>Loads the current schema or returns first-run defaults when no file exists.</summary>
    /// <param name="token">Cancellation.</param>
    /// <returns>Validated data or a precise application failure.</returns>
    public async Task<Result<DesktopPreferences, DesktopError>> LoadAsync(CancellationToken token)
    {
        var loaded = await DesktopEdge
            .TryAsync(
                async () =>
                {
                    token.ThrowIfCancellationRequested();
                    if (!File.Exists(path))
                        return Result.Success<DesktopPreferences, DesktopError>(
                            DesktopPreferences.Default
                        );
                    if (new FileInfo(path).Length > 4096)
                        return Result.Failure<DesktopPreferences, DesktopError>(
                            new("Settings.Size", "The settings file exceeds 4096 bytes.")
                        );
                    return DesktopPreferences.FromJson(
                        await File.ReadAllBytesAsync(path, token).ConfigureAwait(false)
                    );
                },
                "Settings.Read",
                "The saved settings could not be read."
            )
            .ConfigureAwait(false);
        return loaded.Bind(result => result);
    }

    /// <summary>Atomically persists validated configuration.</summary>
    /// <param name="preferences">Validated application configuration.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Validated data or a precise application failure.</returns>
    public async Task<UnitResult<DesktopError>> SaveAsync(
        DesktopPreferences preferences,
        CancellationToken token
    )
    {
        var saved = await DesktopEdge
            .TryAsync(
                () => WriteAsync(preferences, token),
                "Settings.Write",
                "The settings could not be saved."
            )
            .ConfigureAwait(false);
        return saved.IsSuccess
            ? UnitResult.Success<DesktopError>()
            : UnitResult.Failure(saved.Error);
    }

    private async Task<bool> WriteAsync(DesktopPreferences preferences, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var folder = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
        PrivateFileWriter.CreateDirectory(folder);
        var staging = Path.Combine(folder, $".preferences-{Guid.NewGuid():N}.tmp");
        try
        {
            await PrivateFileWriter
                .WriteAsync(staging, preferences.ToJsonBytes(), token)
                .ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(staging, path, overwrite: true);
            return true;
        }
        finally
        {
            if (File.Exists(staging))
                File.Delete(staging);
        }
    }
}
