// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using System.Text.Json;
using CSharpFunctionalExtensions;

internal static class PackageJson
{
    internal const int MaximumBytes = 1024 * 1024;
    internal static JsonSerializerOptions Options { get; } =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = System
                .Text
                .Json
                .Serialization
                .JsonUnmappedMemberHandling
                .Disallow,
            MaxDepth = 32,
        };

    internal static UnitResult<PackageError> Validate(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty || bytes.Length > MaximumBytes)
            return new PackageError(
                PackageErrorCodes.InvalidManifest,
                "Metadata must be a bounded UTF-8 JSON object."
            );
        var owned = bytes.ToArray();
        try
        {
            var result = PackageEdge.Bind(
                () =>
                {
                    using var document = JsonDocument.Parse(
                        owned,
                        new JsonDocumentOptions { MaxDepth = 32 }
                    );
                    return
                        document.RootElement.ValueKind == JsonValueKind.Object
                        && Unique(document.RootElement)
                        ? Result.Success<bool, PackageError>(true)
                        : new PackageError(
                            PackageErrorCodes.InvalidManifest,
                            "Metadata must be an object without duplicate property names."
                        );
                },
                PackageErrorCodes.InvalidManifest,
                "The metadata JSON is malformed."
            );
            return result.IsFailure ? result.Error : UnitResult.Success<PackageError>();
        }
        finally
        {
            Array.Clear(owned);
        }
    }

    private static bool Unique(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => element
                .EnumerateObject()
                .Select(value => value.Name)
                .Distinct(StringComparer.Ordinal)
                .Count() == element.EnumerateObject().Count()
                && element.EnumerateObject().All(value => Unique(value.Value)),
            JsonValueKind.Array => element.EnumerateArray().All(Unique),
            _ => true,
        };
}
