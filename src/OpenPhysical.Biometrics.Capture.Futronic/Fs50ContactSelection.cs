// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal sealed class Fs50ContactSelection : IDisposable
{
    private const int Pixels = 800 * 750;
    private readonly Fs50TemporaryPlane contact = new();
    private readonly Fs50TemporaryPlane observed = new();
    private int selectedArea,
        count,
        exterior,
        largestExterior;
    private Maybe<Fs50RegionStatistics> statistics;
    private Fs50ContactIssue issue;
    internal Maybe<Fs50ContactIssue> Issue => statistics.Map(_ => issue);

    internal bool Consider(
        byte[] weak,
        byte[] chosen,
        Fs50RegionStatistics original,
        Fs50ContactIssue contactIssue,
        Fs50HysteresisGrowth growth
    )
    {
        count += Math.Max(1, growth.PrimarySeeds);
        if (growth.RetainedArea <= selectedArea)
            return false;
        selectedArea = growth.RetainedArea;
        Array.Copy(chosen, contact.Buffer, Pixels);
        Array.Copy(weak, observed.Buffer, Pixels);
        statistics = original;
        issue = contactIssue;
        exterior = growth.UnsupportedExteriorArea;
        largestExterior = growth.LargestUnsupportedExteriorArea;
        return true;
    }

    internal Fs50SpatialContactEvidence Freeze()
    {
        var zero = Fs50DiagnosticPlanes.Empty.Candidate;
        return new(
            selectedArea == 0 ? zero : ImmutableArray.Create(contact.Buffer, 0, Pixels),
            selectedArea == 0 ? zero : ImmutableArray.Create(observed.Buffer, 0, Pixels),
            count,
            statistics,
            issue,
            exterior,
            largestExterior
        );
    }

    public void Dispose()
    {
        contact.Dispose();
        observed.Dispose();
    }
}
