// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;

// The package owns frozen fixture bytes; this adapter follows the consumer's error contract.
internal sealed record Sd04Fixtures(ImmutableArray<Sd04Pair> Pairs)
{
    internal static Result<Sd04Fixtures> Load(string directory) =>
        Sd04FixtureEdge.Run(() => Read(directory));

    private static Result<Sd04Fixtures> Read(string directory)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(directory, "manifest.json"))
        );
        var root = document.RootElement;
        if (root.GetProperty("schema_version").GetInt32() != 1)
            return Result.Failure<Sd04Fixtures>("FixtureSchemaInvalid");
        var images = root.GetProperty("images")
            .EnumerateArray()
            .Select(item => ReadImage(directory, item))
            .ToImmutableDictionary(image => image.Id);
        var pairs = root.GetProperty("pairs")
            .EnumerateArray()
            .Select(item => new Sd04Pair(
                images[item.GetProperty("first").GetString() ?? string.Empty],
                images[item.GetProperty("second").GetString() ?? string.Empty]
            ))
            .ToImmutableArray();
        return Result
            .Success(new Sd04Fixtures(pairs))
            .Ensure(_ => pairs.Length == 10 && images.Count == 20, "FixtureMembershipInvalid");
    }

    private static Sd04Image ReadImage(string directory, JsonElement item) =>
        new(
            item.GetProperty("id").GetString() ?? string.Empty,
            Path.Combine(directory, item.GetProperty("raw").GetString() ?? string.Empty),
            item.GetProperty("raw_sha256").GetString() ?? string.Empty,
            item.GetProperty("width").GetInt32(),
            item.GetProperty("height").GetInt32(),
            item.GetProperty("dots_per_inch").GetInt32()
        );
}
