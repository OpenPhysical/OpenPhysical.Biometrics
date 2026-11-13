// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Persists the whole person's immutable artifacts in one encrypted transaction.</summary>
public interface IEnrollmentPackageStorage
{
    /// <summary>Writes the complete encrypted person package without plaintext staging.</summary>
    /// <param name="package">Complete immutable person artifact set.</param>
    /// <param name="secret">In-memory passphrase owner.</param>
    /// <param name="folder">Private persistent destination.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The opaque encrypted path or a typed failure.</returns>
    Task<Result<string, DesktopError>> SaveAsync(
        BiometricPackage package,
        PackageSecret secret,
        string folder,
        CancellationToken token
    );

    /// <summary>Authenticates and reopens an encrypted person package.</summary>
    /// <param name="path">Encrypted person package path.</param>
    /// <param name="secret">In-memory passphrase owner.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>The authenticated package or a typed failure.</returns>
    Task<Result<BiometricPackage, DesktopError>> OpenAsync(
        string path,
        PackageSecret secret,
        CancellationToken token
    );
}
