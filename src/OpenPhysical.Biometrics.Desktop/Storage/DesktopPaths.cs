// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

internal static class DesktopPaths
{
    internal static string UserData =>
        Path.Combine(
            OperatingSystem.IsMacOS()
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library",
                    "Application Support"
                )
                : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenPhysical"
        );
}
