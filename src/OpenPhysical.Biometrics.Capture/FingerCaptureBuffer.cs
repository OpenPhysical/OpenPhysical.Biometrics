// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal sealed class FingerCaptureBuffer(TimeProvider clock)
{
    private ImmutableArray<(
        FingerPresenceSample Sample,
        FingerCaptureCandidate Candidate
    )> entries = [];
    internal int Count => entries.Length;
    internal FingerCapturePolicy Policy { get; set; } = FingerCapturePolicy.Default;
    internal ImmutableArray<FingerCaptureCandidate> Candidates =>
        entries.Select(entry => entry.Candidate).ToImmutableArray();
    internal Maybe<FingerCaptureCandidate> Best =>
        entries
            .Select(entry => entry.Candidate)
            .OrderBy(candidate => candidate.Motion)
            .ThenBy(candidate => candidate.ClippedFraction)
            .ThenByDescending(candidate => candidate.ForegroundArea)
            .ThenByDescending(candidate => candidate.ObservedAt)
            .Select(Maybe.From)
            .FirstOrDefault();

    internal bool Prune(long now)
    {
        entries = entries
            .Where(entry => clock.GetElapsedTime(entry.Sample.At, now) <= Policy.CandidateWindow)
            .TakeLast(Policy.CandidateLimit)
            .ToImmutableArray();
        return true;
    }

    internal bool Add(FingerPresenceSample sample)
    {
        Prune(sample.At);
        var motion = entries.IsEmpty
            ? double.PositiveInfinity
            : FingerFrameMotion.Score(entries[^1].Sample, sample);
        if (!entries.IsEmpty && double.IsPositiveInfinity(entries[^1].Candidate.Motion))
            entries = entries.SetItem(
                entries.Length - 1,
                (entries[^1].Sample, entries[^1].Candidate with { Motion = motion })
            );
        var candidate = new FingerCaptureCandidate(
            sample.Frame,
            sample.Mask,
            motion,
            sample.Geometry.Clipped,
            sample.Geometry.Area,
            sample.At
        );
        entries = entries
            .TakeLast(Policy.CandidateLimit - 1)
            .Append((sample, candidate))
            .ToImmutableArray();
        return true;
    }

    internal bool Clear()
    {
        entries = [];
        return true;
    }
}
