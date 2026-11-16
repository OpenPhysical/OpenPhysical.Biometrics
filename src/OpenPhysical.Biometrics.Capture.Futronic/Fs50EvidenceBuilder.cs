// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Buffers;
using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal sealed class Fs50EvidenceBuilder(Fs50EvidencePolicy policy) : IDisposable
{
    private const int Pixels = 800 * 750;
    private readonly byte[] candidates = Rent();
    private readonly byte[] support = Rent();
    private readonly byte[] accepted = Rent();
    private readonly Fs50ContactSelection selection = new();
    private readonly byte[] component = Rent();
    private readonly byte[] localSupport = Rent();
    private readonly int[] queue = ArrayPool<int>.Shared.Rent(Pixels);
    private readonly List<Fs50ComponentEvidence> observations = [];
    private int selectedArea,
        acceptedCount;
    internal bool HasCandidates => observations.Count > 0;

    internal bool Visit(ReadOnlySpan<byte> pixels, byte[] texture, byte[] strong)
    {
        for (var i = 0; i < Pixels; i++)
        {
            if (texture[i] == 0)
                continue;
            var count = Fs50ContactComponents.Flood(texture, queue, i);
            if (Fs50ContactComponents.Substantial(queue, count, policy.Options))
                _ = Inspect(pixels, count, strong);
        }
        return true;
    }

    internal Fs50ContactDiagnostics Build(ReadOnlySpan<byte> pixels, Maybe<string> sourceDigest)
    {
        var digest = sourceDigest.HasValue ? sourceDigest.Value : Fs50PolicyDigest.Pixels(pixels);
        if (!HasCandidates)
            return Empty(pixels, digest);
        using var inner = new Fs50TemporaryPlane();
        using var rejected = new Fs50TemporaryPlane();
        var rejectedOutput = rejected.PixelsSpan;
        _ = Fs50MaskMorphology.InnerInto(accepted, policy.Options.InnerGuard, inner.Buffer);
        for (var i = 0; i < Pixels; i++)
            rejectedOutput[i] = (byte)(candidates[i] != 0 && accepted[i] == 0 ? 1 : 0);
        var largest = observations
            .OrderByDescending(observation => observation.Original.Area)
            .First();
        return new Fs50ContactDiagnostics(
            digest,
            policy,
            Planes(inner.Buffer, rejected.Buffer),
            observations.ToImmutableArray(),
            acceptedCount,
            acceptedCount > 0
                ? Fs50ContactIssue.None
                : selection.Issue.GetValueOrDefault(largest.Issue),
            Fs50RegionStatistics.Mask(pixels, accepted),
            largest.Original,
            selection.Freeze()
        );
    }

    private Fs50DiagnosticPlanes Planes(byte[] inner, byte[] rejected) =>
        new(Copy(candidates), Copy(support), Copy(accepted), Copy(rejected), Copy(inner));

    private Fs50ContactDiagnostics Empty(ReadOnlySpan<byte> pixels, string digest)
    {
        var empty = Fs50RegionStatistics.Positions(pixels, []);
        return new(
            digest,
            policy,
            Fs50DiagnosticPlanes.Empty,
            [],
            0,
            Fs50ContactIssue.None,
            empty,
            empty,
            selection.Freeze()
        );
    }

    public void Dispose()
    {
        foreach (var buffer in new[] { candidates, support, accepted, component, localSupport })
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        ArrayPool<int>.Shared.Return(queue, clearArray: true);
        selection.Dispose();
    }

    private bool Inspect(ReadOnlySpan<byte> pixels, int count, byte[] strong)
    {
        Array.Clear(localSupport, 0, Pixels);
        for (var i = 0; i < count; i++)
            candidates[queue[i]] = component[queue[i]] = 1;
        var statistics = Fs50RegionStatistics.Positions(pixels, queue.AsSpan(0, count));
        var hasRidges = Fs50RidgeEvidence.Write(
            pixels,
            component,
            queue,
            count,
            policy.Options,
            localSupport
        );
        var supported = 0;
        for (var i = 0; i < count; i++)
            if (localSupport[queue[i]] != 0)
            {
                support[queue[i]] = 1;
                supported++;
            }
        var issue =
            statistics.Signal < policy.Options.MinimumSignal ? Fs50ContactIssue.TooLight
            : hasRidges ? Fs50ContactIssue.None
            : Fs50ContactIssue.InsufficientRidgeEvidence;
        var retained = hasRidges ? Retain(pixels, count, strong, statistics, issue) : 0;
        if (issue == Fs50ContactIssue.None && retained == 0)
            issue = Fs50ContactIssue.InsufficientRidgeEvidence;
        observations.Add(new Fs50ComponentEvidence(statistics, supported, retained, issue));
        for (var i = 0; i < count; i++)
            component[queue[i]] = 0;
        return true;
    }

    private int Retain(
        ReadOnlySpan<byte> pixels,
        int count,
        byte[] strong,
        Fs50RegionStatistics statistics,
        Fs50ContactIssue issue
    )
    {
        using var chosen = new Fs50TemporaryPlane();
        using var temporary = new Fs50TemporaryPlane();
        var growth = Fs50HysteresisEvidence.Grow(
            pixels,
            component,
            strong,
            queue.AsSpan(0, count),
            localSupport,
            policy.Options,
            chosen.Buffer,
            temporary.Buffer
        );
        var area = growth.RetainedArea;
        if (area < policy.Options.MinimumArea)
            return 0;
        selection.Consider(component, chosen.Buffer, statistics, issue, growth);
        if (issue == Fs50ContactIssue.None)
        {
            acceptedCount += Math.Max(1, growth.PrimarySeeds);
            if (area > selectedArea)
            {
                Array.Copy(temporary.Buffer, accepted, Pixels);
                selectedArea = area;
            }
        }
        return area;
    }

    private static byte[] Rent()
    {
        var bytes = ArrayPool<byte>.Shared.Rent(Pixels);
        Array.Clear(bytes, 0, Pixels);
        return bytes;
    }

    private static ImmutableArray<byte> Copy(byte[] bytes) =>
        ImmutableArray.Create(bytes, 0, Pixels);
}
