// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

internal sealed class SettingsTestFile : IDisposable
{
    internal string Folder { get; } =
        System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "biometrics-settings-" + Guid.NewGuid().ToString("N")
        );
    internal string Path => System.IO.Path.Combine(Folder, "preferences.json");

    public void Dispose()
    {
        if (Directory.Exists(Folder))
            Directory.Delete(Folder, true);
    }
}
