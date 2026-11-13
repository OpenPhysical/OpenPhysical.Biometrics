// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using CSharpFunctionalExtensions;

/// <summary>Validated resource limits independent of biometric modality and device.</summary>
public sealed record PackageProtectionPolicy
{
    private PackageProtectionPolicy(
        int maximumPlaintextBytes,
        int maximumEntryBytes,
        int maximumEntries
    )
    {
        MaximumPlaintextBytes = maximumPlaintextBytes;
        MaximumEntryBytes = maximumEntryBytes;
        MaximumEntries = maximumEntries;
    }

    /// <summary>Gets the 128 MiB archive, 64 MiB entry, sixty-four total entry policy.</summary>
    public static PackageProtectionPolicy Default { get; } =
        new(128 * 1024 * 1024, 64 * 1024 * 1024, 64);

    /// <summary>Gets the maximum authenticated plaintext archive length.</summary>
    public int MaximumPlaintextBytes { get; }

    /// <summary>Gets the maximum uncompressed member length.</summary>
    public int MaximumEntryBytes { get; }

    /// <summary>Gets the maximum ZIP entry count, including the manifest.</summary>
    public int MaximumEntries { get; }

    /// <summary>Creates bounded limits, with absolute ceilings of 512 MiB, 256 MiB per entry, and 256 entries.</summary>
    /// <param name="maximumPlaintextBytes">Archive limit in bytes.</param>
    /// <param name="maximumEntryBytes">Uncompressed member limit in bytes.</param>
    /// <param name="maximumEntries">Total ZIP member limit.</param>
    /// <returns>A resource policy or an invalid-policy error.</returns>
    public static Result<PackageProtectionPolicy, PackageError> Create(
        int maximumPlaintextBytes,
        int maximumEntryBytes,
        int maximumEntries
    ) =>
        maximumPlaintextBytes is >= 128 and <= 512 * 1024 * 1024
        && maximumEntryBytes is >= 1 and <= 256 * 1024 * 1024
        && maximumEntryBytes <= maximumPlaintextBytes
        && maximumEntries is >= 2 and <= 256
            ? new PackageProtectionPolicy(maximumPlaintextBytes, maximumEntryBytes, maximumEntries)
            : new PackageError(
                PackageErrorCodes.InvalidPolicy,
                "Choose valid bounded archive, entry, and member limits."
            );
}
