// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

internal sealed class AlignmentCandidates(AlignmentFootprint footprint)
{
    private readonly List<(
        FingerprintSimilarityTransform Transform,
        int Count,
        double Error
    )> candidates = [];

    internal IEnumerable<FingerprintSimilarityTransform> Transforms =>
        candidates.Select(candidate => candidate.Transform);

    internal bool Consider(
        FingerprintSimilarityTransform transform,
        (int Count, double Error) score
    )
    {
        if (score.Count < 6)
            return false;
        var existing = candidates.FindIndex(candidate =>
            footprint.Equivalent(candidate.Transform, transform)
        );
        if (existing >= 0)
        {
            var previous = candidates[existing];
            if (
                previous.Count > score.Count
                || previous.Count == score.Count && previous.Error <= score.Error
            )
                return false;
            candidates.RemoveAt(existing);
        }
        candidates.Add((transform, score.Count, score.Error));
        candidates.Sort(
            (first, second) =>
                first.Count != second.Count
                    ? second.Count.CompareTo(first.Count)
                    : first.Error.CompareTo(second.Error)
        );
        if (candidates.Count > 12)
            candidates.RemoveAt(12);
        return true;
    }
}
