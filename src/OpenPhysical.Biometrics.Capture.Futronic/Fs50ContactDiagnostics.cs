// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Immutable, original-image evidence; masks use top-left row-major coordinates and one means included.</summary>
public sealed class Fs50ContactDiagnostics
{
    private readonly Fs50DiagnosticPlanes planes;
    private readonly Fs50SpatialContactEvidence spatial;

    internal Fs50ContactDiagnostics(
        string sourceDigest,
        Fs50EvidencePolicy policy,
        Fs50DiagnosticPlanes planes,
        ImmutableArray<Fs50ComponentEvidence> components,
        int count,
        Fs50ContactIssue issue,
        Fs50RegionStatistics accepted,
        Fs50RegionStatistics observed,
        Fs50SpatialContactEvidence spatial
    )
    {
        SourceDigest = sourceDigest;
        Policy = policy;
        this.planes = planes;
        Components = components;
        ComponentCount = count;
        Issue = issue;
        AcceptedStatistics = accepted;
        ObservedStatistics = observed;
        this.spatial = spatial;
        CandidateArea = components.Sum(component => component.Original.Area);
        RidgeSupportArea = components.Sum(component => component.SupportedArea);
    }

    /// <summary>Gets original Gray8 SHA-256 associating all evidence with its exact source.</summary>
    public string SourceDigest { get; }

    /// <summary>Gets the complete validated evidence policy.</summary>
    public Fs50EvidencePolicy Policy { get; }

    /// <summary>Gets the complete evidence policy SHA-256.</summary>
    public string PolicyDigest => Policy.Digest;

    /// <summary>Gets the contact-evidence algorithm identifier.</summary>
    public string Version => Fs50ContactAnalysis.Version;

    /// <summary>Gets substantial texture candidates before darkness or ridge rejection.</summary>
    public ImmutableArray<byte> CandidateMask => planes.Candidate;

    /// <summary>Gets the selected connected texture component for internal presence and motion estimates.</summary>
    public ImmutableArray<byte> ContactMask => spatial.Contact;

    /// <summary>Gets the full selected weak component, including unsupported exterior evidence.</summary>
    public ImmutableArray<byte> ObservedContactMask => spatial.Observed;

    /// <summary>Gets coherent spatial candidates independently of capture darkness.</summary>
    public int SpatialComponentCount => spatial.ComponentCount;

    /// <summary>Gets unchanged selected weak-component statistics, including light contact.</summary>
    public Maybe<Fs50RegionStatistics> SpatialStatistics => spatial.Statistics;

    /// <summary>Gets unsupported exterior area in the full selected weak component.</summary>
    public int RidgeExteriorArea => spatial.RidgeExteriorArea;

    /// <summary>Gets the largest connected unsupported exterior region, without thresholding.</summary>
    public int LargestRidgeExteriorArea => spatial.LargestRidgeExteriorArea;

    /// <summary>Gets pixels within locally supported ridge neighborhoods before expansion.</summary>
    public ImmutableArray<byte> RidgeSupportMask => planes.RidgeSupport;

    /// <summary>Gets retained supported neighborhoods intersected with contact.</summary>
    public ImmutableArray<byte> AcceptedMask => planes.Accepted;

    /// <summary>Gets candidate pixels excluded from the retained image area.</summary>
    public ImmutableArray<byte> RejectedMask => planes.Rejected;

    /// <summary>Gets the eroded area permitted to retain extracted minutiae.</summary>
    public ImmutableArray<byte> InnerMask => planes.Inner;

    /// <summary>Gets unenhanced evidence for each substantial candidate, including rejected candidates.</summary>
    public ImmutableArray<Fs50ComponentEvidence> Components { get; }

    /// <summary>Gets the combined substantial candidate area.</summary>
    public int CandidateArea { get; }

    /// <summary>Gets local ridge-support area before expansion.</summary>
    public int RidgeSupportArea { get; }

    /// <summary>Gets retained reliable image area used in candidate selection.</summary>
    public int SupportedArea => AcceptedStatistics.Area;

    /// <summary>Gets retained image area.</summary>
    public int AcceptedArea => AcceptedStatistics.Area;

    /// <summary>Gets excluded candidate area.</summary>
    public int RejectedArea => CandidateArea - AcceptedArea;

    /// <summary>Gets accepted candidate count before selecting the largest retained region.</summary>
    public int ComponentCount { get; }

    /// <summary>Gets rejection reason when no candidate qualifies.</summary>
    public Fs50ContactIssue Issue { get; }

    /// <summary>Gets photometric evidence from the selected retained region.</summary>
    public Fs50RegionStatistics AcceptedStatistics { get; }

    /// <summary>Gets original evidence from the largest observed candidate, even if rejected.</summary>
    public Fs50RegionStatistics ObservedStatistics { get; }

    /// <summary>Gets original observed contrast without losing rejected evidence.</summary>
    public double Contrast => ObservedStatistics.Contrast;

    /// <summary>Gets original observed darkness without losing rejected evidence.</summary>
    public double Signal => ObservedStatistics.Signal;
}
