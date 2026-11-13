// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal static class PersonPackageWriter
{
    internal static async Task<Result<string, DesktopError>> SaveAsync(
        ExportViewModel export,
        IEnrollmentPackageStorage storage,
        string personId,
        CancellationToken token
    )
    {
        var secret = await export.Protection.RequireAsync(true);
        if (secret.IsFailure)
            return secret.Error;
        var package = PersonPackageBuilder.Create(personId, export.Enrollment);
        if (package.IsFailure)
            return package.Error;
        return await storage.SaveAsync(package.Value, secret.Value, export.Folder, token);
    }
}
