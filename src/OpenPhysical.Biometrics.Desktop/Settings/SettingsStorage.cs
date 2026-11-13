// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

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
            .TryOptionalFileAsync(
                async () =>
                {
                    token.ThrowIfCancellationRequested();
                    if ((File.GetAttributes(path) & FileAttributes.Directory) != 0)
                        return Result.Failure<DesktopPreferences, DesktopError>(
                            new(
                                "Settings.Read",
                                "The settings path is a directory instead of a file."
                            )
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
        return loaded.Bind(value =>
            value.Match(
                result => result,
                () => Result.Success<DesktopPreferences, DesktopError>(DesktopPreferences.Default)
            )
        );
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
        var saved = await PrivateFileTransaction
            .WriteBatchAsync(
                [new PrivateFileWrite(path, [.. preferences.ToJsonBytes()])],
                true,
                token
            )
            .ConfigureAwait(false);
        return saved.MapError(error => new DesktopError(
            error.Code == PackageErrorCodes.Cancelled ? error.Code : "Settings.Write",
            error.Message
        ));
    }
}
