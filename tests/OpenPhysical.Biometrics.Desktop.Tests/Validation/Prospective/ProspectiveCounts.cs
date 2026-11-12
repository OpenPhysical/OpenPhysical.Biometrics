// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;

internal sealed record ProspectiveCounts(
    int GenuineAccepted,
    int ImpostorAccepted,
    int BetweenPersonAccepted,
    int WithinPersonDifferentFingerAccepted,
    int AvailableGenuinePairs,
    int UnavailableGenuinePairs,
    int AvailableImpostorPairs,
    int UnavailableImpostorPairs,
    int AvailableBetweenPersonPairs,
    int UnavailableBetweenPersonPairs,
    int AvailableWithinPersonDifferentFingerPairs,
    int UnavailableWithinPersonDifferentFingerPairs,
    ImmutableArray<ProspectiveUnavailable> UnavailableImages
)
{
    internal static ProspectiveCounts Create(
        ImmutableArray<ProspectiveImage> images,
        ImmutableArray<ProspectivePair> pairs,
        ProspectiveArm arm
    )
    {
        var unavailable = images
            .Where(image => image.For(arm).Template.HasNoValue)
            .Select(image => new ProspectiveUnavailable(
                image.Id,
                ProspectiveFailure.Classify(image.For(arm).Rejection.Value).Value,
                image.For(arm).Rejection.Value.Code
            ))
            .ToImmutableArray();
        var genuine = pairs.Where(pair => pair.Genuine).ToArray();
        var impostor = pairs.Where(pair => !pair.Genuine).ToArray();
        var between = pairs.Where(pair => pair.BetweenPerson).ToArray();
        var within = pairs.Where(pair => !pair.Genuine && !pair.BetweenPerson).ToArray();
        return new(
            Accepts(genuine, arm),
            Accepts(impostor, arm),
            Accepts(between, arm),
            Accepts(within, arm),
            Available(genuine, arm),
            genuine.Length - Available(genuine, arm),
            Available(impostor, arm),
            impostor.Length - Available(impostor, arm),
            Available(between, arm),
            between.Length - Available(between, arm),
            Available(within, arm),
            within.Length - Available(within, arm),
            unavailable
        );
    }

    private static int Available(IEnumerable<ProspectivePair> pairs, ProspectiveArm arm) =>
        pairs.Count(pair => pair.Score(arm).HasValue);

    private static int Accepts(IEnumerable<ProspectivePair> pairs, ProspectiveArm arm) =>
        pairs.Count(pair => pair.Score(arm).HasValue && pair.Score(arm).Value >= 40);
}
