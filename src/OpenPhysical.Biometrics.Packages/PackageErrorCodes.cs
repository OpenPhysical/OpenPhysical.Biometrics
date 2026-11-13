// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

/// <summary>Stable errors for private package operations.</summary>
public static class PackageErrorCodes
{
    /// <summary>Passphrase encoding or length is invalid.</summary>
    public const string InvalidSecret = "Package.InvalidSecret";

    /// <summary>The owned passphrase has been cleared.</summary>
    public const string SecretDisposed = "Package.SecretDisposed";

    /// <summary>Public envelope structure or KDF bounds are invalid.</summary>
    public const string InvalidEnvelope = "Package.InvalidEnvelope";

    /// <summary>Authentication failed; passphrase and tampering failures are indistinguishable.</summary>
    public const string AuthenticationFailed = "Package.AuthenticationFailed";

    /// <summary>Package metadata is invalid.</summary>
    public const string InvalidManifest = "Package.InvalidManifest";

    /// <summary>An artifact name or description is invalid.</summary>
    public const string InvalidArtifact = "Package.InvalidArtifact";

    /// <summary>The authenticated archive is malformed or unsafe.</summary>
    public const string InvalidArchive = "Package.InvalidArchive";

    /// <summary>Archive members disagree with authenticated manifest evidence.</summary>
    public const string ArtifactMismatch = "Package.ArtifactMismatch";

    /// <summary>An operation exceeds configured resource bounds.</summary>
    public const string LimitExceeded = "Package.LimitExceeded";

    /// <summary>The resource policy is invalid.</summary>
    public const string InvalidPolicy = "Package.InvalidPolicy";

    /// <summary>The cryptographic provider failed.</summary>
    public const string CryptoFailed = "Package.CryptoFailed";

    /// <summary>A file destination is a repository, link, or invalid path.</summary>
    public const string UnsafePath = "Package.UnsafePath";

    /// <summary>An existing destination was not authorized for replacement.</summary>
    public const string DestinationExists = "Package.DestinationExists";

    /// <summary>A private file operation failed.</summary>
    public const string FileAccessFailed = "Package.FileAccessFailed";

    /// <summary>The caller cancelled the operation.</summary>
    public const string Cancelled = "Cancelled";
}
