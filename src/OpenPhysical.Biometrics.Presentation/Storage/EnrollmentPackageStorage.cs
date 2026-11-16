// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Uses the portable shared package codec and private ciphertext transaction.</summary>
public sealed class EnrollmentPackageStorage : IEnrollmentPackageStorage
{
    /// <inheritdoc/>
    public async Task<Result<string, DesktopError>> SaveAsync(
        BiometricPackage package,
        PackageSecret secret,
        string folder,
        CancellationToken token
    )
    {
        var result = await PackageFileStore.SaveAsync(
            folder,
            package,
            secret,
            PackageProtectionPolicy.Default,
            token
        );
        return result.MapError(error => new DesktopError(error.Code, error.Message));
    }

    /// <inheritdoc/>
    public async Task<Result<BiometricPackage, DesktopError>> OpenAsync(
        string path,
        PackageSecret secret,
        CancellationToken token
    )
    {
        var result = await PackageFileStore.OpenAsync(
            path,
            secret,
            PackageProtectionPolicy.Default,
            token
        );
        return result.MapError(error => new DesktopError(error.Code, error.Message));
    }
}
