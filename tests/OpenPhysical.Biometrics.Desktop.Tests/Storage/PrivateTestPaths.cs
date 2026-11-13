// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

internal static class PrivateTestPaths
{
    internal static string Root => OperatingSystem.IsMacOS() ? "/private/tmp" : Path.GetTempPath();
}
