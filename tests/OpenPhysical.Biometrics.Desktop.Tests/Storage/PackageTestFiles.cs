// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using Org.BouncyCastle.Crypto.Digests;

internal static class PackageTestFiles
{
    internal static async Task<(string Folder, string Path, JsonNode Manifest)> SaveAsync(
        CapturedFingerprint capture
    ) => await SaveAsync(capture, TemplateSaveOptions.Default);

    internal static async Task<(string Folder, string Path, JsonNode Manifest)> SaveAsync(
        CapturedFingerprint capture,
        TemplateSaveOptions options
    )
    {
        var folder = ProcessingStorageFixtures.Folder();
        var saved = await new TemplateStorage().SaveAsync(
            UnsignedTemplateService.Create([capture], new(1, 0, 0)).Value,
            [capture],
            folder,
            options,
            CancellationToken.None
        );
        saved.Should().Succeed();
        var manifest = JsonNode.Parse(
            await File.ReadAllTextAsync(Path.ChangeExtension(saved.Value, ".json"))
        )!;
        return (folder, saved.Value, manifest);
    }

    internal static async Task WriteAsync(string path, JsonNode manifest) =>
        await File.WriteAllTextAsync(Path.ChangeExtension(path, ".json"), manifest.ToJsonString());

    internal static bool AssertAdvertisedPngs(string directory, JsonNode manifest)
    {
        var advertised = FileNames(manifest)
            .Where(name => name.EndsWith(".png", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal);
        System
            .IO.Directory.GetFiles(directory, "*.png")
            .Select(Path.GetFileName)
            .Should()
            .BeEquivalentTo(advertised);
        return true;
    }

    private static IEnumerable<string> FileNames(JsonNode node)
    {
        if (node is JsonObject value)
            foreach (var property in value)
            {
                if (
                    property.Key == "FileName"
                    && property.Value is JsonValue scalar
                    && scalar.TryGetValue<string>(out var name)
                    && name is not null
                )
                    yield return name;
                else if (property.Value is { } child)
                    foreach (var file in FileNames(child))
                        yield return file;
            }
        else if (node is JsonArray array)
            foreach (var child in array.OfType<JsonNode>())
            foreach (var file in FileNames(child))
                yield return file;
    }

    internal static string Digest(byte[] bytes)
    {
        var hash = new Sha256Digest();
        hash.BlockUpdate(bytes, 0, bytes.Length);
        var result = new byte[hash.GetDigestSize()];
        hash.DoFinal(result, 0);
        return Convert.ToHexStringLower(result);
    }

    internal static string SettingsDigest(JsonNode processing) =>
        Digest(
            JsonSerializer.SerializeToUtf8Bytes(
                new
                {
                    AlgorithmVersion = processing["AlgorithmVersion"]!.GetValue<string>(),
                    Policy = JsonSerializer.SerializeToElement(processing["Policy"]),
                    SelectionPolicy = JsonSerializer.SerializeToElement(
                        processing["SelectionPolicy"]
                    ),
                    InputRole = processing["InputRole"] is { } role
                        ? role.GetValue<string>()
                        : string.Empty,
                    OriginalQualification = Canonical(processing, "OriginalQualification"),
                    OriginalAcquisitionPolicy = Canonical(processing, "OriginalAcquisitionPolicy"),
                    ContactDiagnostics = Canonical(processing, "ContactDiagnostics"),
                    Contour = Canonical(processing, "Contour"),
                }
            )
        );

    private static string Canonical(JsonNode parent, string key) =>
        !parent.AsObject().ContainsKey(key) ? string.Empty
        : parent[key] is { } value ? value.ToJsonString()
        : "null";
}
