// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.NBIS.Fixtures;

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

// Shared only by the test and example projects; paths and pairing come from one manifest.
internal sealed class Sd04Fixtures
{
    internal Sd04Fixtures(string directory)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(directory, "manifest.json"))
        );
        var root = document.RootElement;
        if (root.GetProperty("schema_version").GetInt32() != 1)
            throw new InvalidDataException("Unsupported fixture manifest.");
        var images = root.GetProperty("images")
            .EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("id").GetString()!,
                item => new Sd04Image(
                    item.GetProperty("id").GetString()!,
                    Path.Combine(directory, item.GetProperty("raw").GetString()!),
                    item.GetProperty("raw_sha256").GetString()!,
                    item.GetProperty("width").GetInt32(),
                    item.GetProperty("height").GetInt32(),
                    item.GetProperty("dots_per_inch").GetInt32()
                )
            );
        Pairs = root.GetProperty("pairs")
            .EnumerateArray()
            .Select(item => new Sd04Pair(
                item.GetProperty("id").GetString()!,
                item.GetProperty("primary_class").GetString()!,
                item.GetProperty("cross_referenced_class").GetString(),
                images[item.GetProperty("first").GetString()!],
                images[item.GetProperty("second").GetString()!]
            ))
            .ToImmutableArray();
        if (Pairs.Length != 10 || images.Count != 20)
            throw new InvalidDataException("Incomplete SD04 fixture collection.");
    }

    internal ImmutableArray<Sd04Pair> Pairs { get; }
}

internal sealed record Sd04Pair(
    string Id,
    string PrimaryClass,
    string? CrossReferencedClass,
    Sd04Image First,
    Sd04Image Second
);

internal sealed record Sd04Image(
    string Id,
    string RawPath,
    string Sha256,
    int Width,
    int Height,
    int DotsPerInch
)
{
    internal FingerprintImage Load()
    {
        var pixels = File.ReadAllBytes(RawPath);
        try
        {
            if (
                !Convert
                    .ToHexString(SHA256.HashData(pixels))
                    .Equals(Sha256, StringComparison.OrdinalIgnoreCase)
            )
                throw new InvalidDataException("Fixture pixel checksum mismatch.");
            return new FingerprintImage(pixels, Width, Height, DotsPerInch);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pixels);
        }
    }
}
