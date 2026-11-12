// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;

internal sealed record ProspectiveLedger(
    int ImageCount,
    ImmutableArray<ProspectiveIdentity> Images,
    ImmutableArray<ProspectiveLedgerPair> Pairs,
    int GenuinePairs,
    int ImpostorPairs,
    int BetweenPersonPairs,
    int WithinPersonDifferentFingerPairs,
    ProspectiveCounts Original,
    ProspectiveCounts Processed,
    ImmutableDictionary<string, string> SourceHashes,
    int GenuineGained,
    int GenuineLost,
    string CandidateArm
)
{
    public int SchemaVersion => 2;
    public string Protocol => "prospective-full-attempts-v2";
    public int Threshold => 40;
    public bool Passed =>
        Processed.GenuineAccepted >= Original.GenuineAccepted
        && Processed.ImpostorAccepted <= Original.ImpostorAccepted
        && Processed.BetweenPersonAccepted <= Original.BetweenPersonAccepted
        && Processed.WithinPersonDifferentFingerAccepted
            <= Original.WithinPersonDifferentFingerAccepted;
    public int UnavailableOriginalImages => Original.UnavailableImages.Length;
    public int UnavailableProcessedImages => Processed.UnavailableImages.Length;
    public ImmutableArray<DesktopError> OperationalErrors => [];
    public string FrozenProtocolDigest { get; init; } = string.Empty;
    public string CohortManifestDigest { get; init; } = string.Empty;

    internal static ProspectiveLedger Create(
        ImmutableArray<ProspectiveImage> images,
        ImmutableArray<ProspectivePair> pairs,
        ProspectiveArm candidate,
        ImmutableDictionary<string, string> hashes
    ) =>
        new(
            images.Length,
            images
                .Select(image => new ProspectiveIdentity(image.Id, image.PersonId, image.FingerId))
                .ToImmutableArray(),
            pairs
                .Select(pair => new ProspectiveLedgerPair(
                    pair.LeftId,
                    pair.RightId,
                    pair.Score(ProspectiveArm.RawWhole),
                    pair.Score(candidate)
                ))
                .ToImmutableArray(),
            pairs.Count(pair => pair.Genuine),
            pairs.Count(pair => !pair.Genuine),
            pairs.Count(pair => pair.BetweenPerson),
            pairs.Count(pair => !pair.Genuine && !pair.BetweenPerson),
            ProspectiveCounts.Create(images, pairs, ProspectiveArm.RawWhole),
            ProspectiveCounts.Create(images, pairs, candidate),
            hashes,
            pairs.Count(pair =>
                pair.Genuine
                && !Accepted(pair, ProspectiveArm.RawWhole)
                && Accepted(pair, candidate)
            ),
            pairs.Count(pair =>
                pair.Genuine
                && Accepted(pair, ProspectiveArm.RawWhole)
                && !Accepted(pair, candidate)
            ),
            candidate.ToString()
        );

    private static bool Accepted(ProspectivePair pair, ProspectiveArm arm) =>
        pair.Score(arm).HasValue && pair.Score(arm).Value >= 40;
}

internal sealed record ProspectiveIdentity(string Id, string PersonId, string FingerId);
