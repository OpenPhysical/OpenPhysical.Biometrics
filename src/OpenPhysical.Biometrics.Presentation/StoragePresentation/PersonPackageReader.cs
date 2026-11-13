// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Imports the existing authenticated encrypted package without capture services.</summary>
public static class PersonPackageReader
{
    /// <summary>Prompts for a file and its passphrase, retaining no plaintext file.</summary>
    /// <param name="picker">Application-owned file picker.</param>
    /// <param name="protection">Transient package protection session.</param>
    /// <param name="storage">Existing package decoder and storage.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The unchanged producer package or a typed failure.</returns>
    public static async Task<Result<BiometricPackage, DesktopError>> OpenAsync(
        DesktopFilePicker picker,
        PackageProtectionViewModel protection,
        IEnrollmentPackageStorage storage,
        CancellationToken token
    )
    {
        var path = await picker.OpenPackageAsync();
        if (path.IsFailure)
            return path.Error;
        return await OpenPathAsync(path.Value, protection, storage, token);
    }

    /// <summary>Authenticates a selected encrypted file using the existing transient passphrase prompt.</summary>
    /// <param name="path">User-selected encrypted file.</param>
    /// <param name="protection">Transient package protection.</param>
    /// <param name="storage">Existing authenticated package decoder.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The unchanged producer package or a typed failure.</returns>
    public static async Task<Result<BiometricPackage, DesktopError>> OpenPathAsync(
        string path,
        PackageProtectionViewModel protection,
        IEnrollmentPackageStorage storage,
        CancellationToken token
    )
    {
        var secret = await protection.RequireAsync(false);
        if (secret.IsFailure)
            return secret.Error;
        var revision = protection.Revision;
        var opened = await storage.OpenAsync(path, secret.Value, token);
        if (token.IsCancellationRequested || revision != protection.Revision)
            return new DesktopError(
                "Package.Locked",
                "Package protection was locked during opening."
            );
        if (opened.IsFailure && opened.Error.Code == PackageErrorCodes.AuthenticationFailed)
            protection.Lock();
        return opened;
    }
}
