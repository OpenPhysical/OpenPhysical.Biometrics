// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

internal static class EnrollmentExportFixtures
{
    private const string Passphrase = "public-fixture-package-secret";

    internal static ExportViewModel Create(
        CaptureViewModel capture,
        IEnrollmentPackageStorage storage,
        EnrollmentFingerSuggestions suggestions,
        InspectionViewModel inspection
    ) =>
        new(
            capture,
            storage,
            suggestions,
            inspection,
            new PersonEnrollmentViewModel(),
            [],
            UnlockedProtection(),
            new DesktopFilePicker(),
            Maybe<CameraSetupViewModel>.None
        );

    internal static string Folder() =>
        Path.Combine(PrivateTestPaths.Root, "encrypted-person-" + Guid.NewGuid().ToString("N"));

    internal static PackageSecret Secret() =>
        PackageSecret.Create(Encoding.UTF8.GetBytes(Passphrase)).Value;

    internal static PackageProtectionViewModel UnlockedProtection()
    {
        var value = new PackageProtectionViewModel
        {
            Passphrase = Passphrase,
            Confirmation = Passphrase,
        };
        _ = value.Unlock();
        return value;
    }
}
