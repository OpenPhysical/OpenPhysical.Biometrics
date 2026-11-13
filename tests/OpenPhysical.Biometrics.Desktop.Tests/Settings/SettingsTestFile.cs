// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

internal sealed class SettingsTestFile : IDisposable
{
    internal string Folder { get; } =
        System.IO.Path.Combine(
            PrivateTestPaths.Root,
            "biometrics-settings-" + Guid.NewGuid().ToString("N")
        );
    internal string Path => System.IO.Path.Combine(Folder, "preferences.json");

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Folder))
            System.IO.Directory.Delete(Folder, true);
    }
}
