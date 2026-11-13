// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Owns one opaque unsigned object or companion and its declared interchange format.</summary>
public sealed record PackageArtifact
{
    private PackageArtifact(string name, ImmutableArray<byte> bytes, string role, string format)
    {
        Name = name;
        Bytes = bytes;
        Role = role;
        Format = format;
    }

    /// <summary>Gets the portable relative ZIP member name.</summary>
    public string Name { get; }

    /// <summary>Gets immutable exact artifact bytes.</summary>
    public ImmutableArray<byte> Bytes { get; }

    /// <summary>Gets the application-defined artifact role.</summary>
    public string Role { get; }

    /// <summary>Gets the application-defined interchange format identifier.</summary>
    public string Format { get; }

    /// <summary>Gets the lowercase SHA-256 of the exact bytes.</summary>
    public string Sha256 => PackageHash.Of(Bytes.AsSpan());

    /// <summary>Copies one unsigned object or companion without interpreting its modality codec.</summary>
    /// <param name="name">Portable relative member name.</param>
    /// <param name="bytes">Exact contents copied into immutable ownership.</param>
    /// <param name="role">Artifact purpose, such as face or fingerprint source image.</param>
    /// <param name="format">Interchange identifier supplied by the owning codec.</param>
    /// <returns>An owned artifact or a typed name/description failure.</returns>
    public static Result<PackageArtifact, PackageError> Create(
        string name,
        ReadOnlySpan<byte> bytes,
        string role,
        string format
    ) =>
        PackageNames.Valid(name)
        && !string.IsNullOrWhiteSpace(role)
        && role.Length <= 128
        && !string.IsNullOrWhiteSpace(format)
        && format.Length <= 128
            ? new PackageArtifact(name, ImmutableArray.Create(bytes), role, format)
            : new PackageError(
                PackageErrorCodes.InvalidArtifact,
                "Use a portable unique artifact name and nonempty role and format."
            );
}
