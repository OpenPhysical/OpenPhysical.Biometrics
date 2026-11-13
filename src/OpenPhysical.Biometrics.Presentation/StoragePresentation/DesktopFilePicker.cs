// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Opens local reference templates with the application window as owner.</summary>
public sealed class DesktopFilePicker
{
    private Maybe<Window> window;

    /// <summary>Assigns the native dialog owner.</summary>
    /// <param name="owner">Application window.</param>
    /// <returns>Successful owner assignment.</returns>
    public UnitResult<DesktopError> Attach(Window owner)
    {
        window = owner;
        return UnitResult.Success<DesktopError>();
    }

    /// <summary>Requests one local fingerprint reference file.</summary>
    /// <returns>The selected local path or a typed cancellation or dialog failure.</returns>
    public Task<Result<string, DesktopError>> OpenAsync() =>
        PickAsync("Open fingerprint template", "Fingerprint templates", ["*.fmr", "*.bin"]);

    /// <summary>Requests an encrypted person package without extracting any plaintext files.</summary>
    /// <returns>The local encrypted package path or a typed cancellation.</returns>
    public Task<Result<string, DesktopError>> OpenPackageAsync() =>
        PickAsync("Open person package", "Encrypted biometric packages", ["*.opbio"]);

    private async Task<Result<string, DesktopError>> PickAsync(
        string title,
        string label,
        string[] patterns
    )
    {
        if (window.HasNoValue)
            return new DesktopError("WindowMissing", "The file picker is unavailable.");
        var result = await DesktopEdge.TryAsync(
            async () =>
                await window.Value.StorageProvider.OpenFilePickerAsync(
                    new FilePickerOpenOptions
                    {
                        Title = title,
                        AllowMultiple = false,
                        FileTypeFilter =
                        [
                            new FilePickerFileType(label) { Patterns = patterns },
                            FilePickerFileTypes.All,
                        ],
                    }
                ),
            "OpenFile",
            "Cannot open file picker."
        );
        if (result.IsFailure)
            return result.Error;
        if (result.Value.Count != 1)
            return new DesktopError("Cancelled", "No file selected.");
        return result.Value[0].Path.IsFile
            ? result.Value[0].Path.LocalPath
            : new DesktopError("LocalFileRequired", "Select a local file.");
    }
}
