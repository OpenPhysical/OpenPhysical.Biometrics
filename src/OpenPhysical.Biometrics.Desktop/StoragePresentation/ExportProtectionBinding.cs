// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal static class ExportProtectionBinding
{
    internal static UnitResult<DesktopError> Attach(ExportViewModel export)
    {
        export.Protection.PropertyChanged += (_, args) =>
        {
            if (
                args.PropertyName != nameof(PackageProtectionViewModel.IsUnlocked)
                || export.Protection.IsUnlocked
            )
                return;
            export.OpenCommand.Cancel();
            export.SaveCommand.Cancel();
            export.ForgetOpenedPackage();
        };
        return UnitResult.Success<DesktopError>();
    }
}
