// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

internal static class ProcessingStorageFixtures
{
    internal static CapturedFingerprint Capture() => HullStorageFixtures.Capture(true);

    internal static string Folder() =>
        Path.Combine(Path.GetTempPath(), "processing-package-" + Guid.NewGuid().ToString("N"));
}
