// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using System.Text.Json;
using CSharpFunctionalExtensions;

internal sealed record ProspectiveFrozenProtocol(
    string Digest,
    string ManifestDigest,
    string Role,
    bool FinalAuthorized,
    ImmutableDictionary<string, string> SourceHashes,
    ImmutableArray<ProspectiveArm> Arms
)
{
    internal static Result<ProspectiveFrozenProtocol, DesktopError> Read(
        string path,
        string repo,
        string manifestDigest,
        string role
    ) =>
        EvaluationEdge
            .Try(
                () => ReadCore(path, repo, manifestDigest, role),
                "Evaluation.Protocol",
                "Cannot read the frozen prospective protocol."
            )
            .Bind(value => value);

    private static Result<ProspectiveFrozenProtocol, DesktopError> ReadCore(
        string path,
        string repo,
        string manifestDigest,
        string role
    )
    {
        var bytes = File.ReadAllBytes(path);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (!Matches(root, manifestDigest, role))
            return Invalid();
        return Access(role, root.GetProperty("FinalAuthorized").GetBoolean())
            .Map(() =>
                root.GetProperty("Arms")
                    .EnumerateArray()
                    .Select(value => Enum.Parse<ProspectiveArm>(value.GetString()!))
                    .ToImmutableArray()
            )
            .Bind(arms =>
                VerifyHashes(root, repo)
                    .Map(hashes => new ProspectiveFrozenProtocol(
                        EvaluationDigest.Of(bytes).ToLowerInvariant(),
                        manifestDigest,
                        role,
                        root.GetProperty("FinalAuthorized").GetBoolean(),
                        hashes,
                        arms
                    ))
            );
    }

    private static Result<ImmutableDictionary<string, string>, DesktopError> VerifyHashes(
        JsonElement root,
        string repo
    )
    {
        var hashes = root.GetProperty("SourceHashes")
            .Deserialize<ImmutableDictionary<string, string>>()!;
        if (hashes.IsEmpty)
            return Invalid();
        foreach (var pair in hashes)
        {
            var source = Path.GetFullPath(Path.Combine(repo, pair.Key));
            if (
                !source.StartsWith(
                    Path.GetFullPath(repo) + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal
                )
                || !EvaluationDigest.Same(
                    EvaluationDigest.Of(File.ReadAllBytes(source)),
                    pair.Value
                )
            )
                return Invalid();
        }
        foreach (var hash in root.GetProperty("ExecutionHashes").EnumerateArray())
            if (
                !EvaluationDigest.Same(
                    EvaluationDigest.Of(File.ReadAllBytes(hash.GetProperty("Path").GetString()!)),
                    hash.GetProperty("Sha256").GetString()!
                )
            )
                return Invalid();
        return hashes;
    }

    private static bool Matches(JsonElement root, string digest, string role) =>
        root.GetProperty("SchemaVersion").GetInt32() == 2
        && root.GetProperty("Threshold").GetInt32() == 40
        && root.GetProperty("Protocol").GetString() == "prospective-full-attempts-v2"
        && EvaluationDigest.Same(root.GetProperty("CohortManifestDigest").GetString()!, digest)
        && root.GetProperty("Role").GetString() == role
        && root.GetProperty("Arms")
            .EnumerateArray()
            .Select(value => value.GetString())
            .SequenceEqual(Enum.GetNames<ProspectiveArm>());

    internal static UnitResult<DesktopError> Access(string role, bool finalAuthorized) =>
        role == "development" || (role == "final" && finalAuthorized)
            ? UnitResult.Success<DesktopError>()
            : UnitResult.Failure(
                new DesktopError(
                    "Evaluation.ReservedCohort",
                    "Only development data or an explicitly released frozen final cohort may enter this runner."
                )
            );

    private static DesktopError Invalid() =>
        new(
            "Evaluation.Protocol",
            "The exact candidate options, source or cohort differs from the frozen prospective protocol."
        );
}
