// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal static class EnrollmentWorkspaceReset
{
    internal const string ProductOwner = "0x0103";
    internal const string Unreported = "0";

    internal static async Task<UnitResult<DesktopError>> ApplyAsync(
        ExportViewModel export,
        Maybe<InspectionViewModel> inspection
    )
    {
        export.ProductOwner = ProductOwner;
        export.ProductType = Unreported;
        export.EquipmentId = Unreported;
        export.Folder = TemplateStorage.DefaultFolder;
        export.IncludeDiagnostics = false;
        var reset = await export.Enrollment.ResetAsync();
        var reference = inspection.HasValue
            ? await inspection.Value.ClearPersonAsync(export.Enrollment.Fingerprints.PrimaryFinger)
            : UnitResult.Success<DesktopError>();
        return reset.IsFailure ? reset : reference;
    }
}
