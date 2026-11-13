// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;

internal sealed record PackageManifest(
    int Version,
    string PersonId,
    string Label,
    string ExternalId,
    JsonElement Evidence,
    ImmutableArray<PackageManifestArtifact> Artifacts
)
{
    internal static byte[] Write(BiometricPackage package)
    {
        using var evidence = JsonDocument.Parse(package.EvidenceJson.AsMemory());
        return JsonSerializer.SerializeToUtf8Bytes(
            new PackageManifest(
                1,
                package.PersonId,
                package.Label,
                package.ExternalId.GetValueOrDefault(string.Empty),
                evidence.RootElement,
                [
                    .. package.Artifacts.Select(value => new PackageManifestArtifact(
                        value.Name,
                        value.Role,
                        value.Format,
                        value.Bytes.Length,
                        value.Sha256
                    )),
                ]
            ),
            PackageJson.Options
        );
    }

    internal static Result<PackageManifest, PackageError> Read(byte[] bytes)
    {
        var valid = PackageJson.Validate(bytes);
        if (valid.IsFailure)
            return valid.Error;
        return PackageEdge.Bind(
            () =>
                Maybe
                    .From(JsonSerializer.Deserialize<PackageManifest>(bytes, PackageJson.Options))
                    .ToResult(
                        new PackageError(
                            PackageErrorCodes.InvalidManifest,
                            "A package manifest is required."
                        )
                    )
                    .Ensure(
                        value =>
                            value.Version == 1
                            && value.Evidence.ValueKind == JsonValueKind.Object
                            && !value.Artifacts.IsDefaultOrEmpty
                            && value.Artifacts.All(item => Maybe.From(item).HasValue),
                        new PackageError(
                            PackageErrorCodes.InvalidManifest,
                            "The manifest version, evidence, or artifact list is invalid."
                        )
                    ),
            PackageErrorCodes.InvalidManifest,
            "The package manifest is malformed."
        );
    }
}
