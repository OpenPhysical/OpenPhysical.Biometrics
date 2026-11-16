// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Immutable original-region diagnostics retained even when capture is rejected.</summary>
public sealed record Fs50ComponentEvidence
{
    internal Fs50ComponentEvidence(
        Fs50RegionStatistics original,
        int supportedArea,
        int retainedArea,
        Fs50ContactIssue issue
    )
    {
        Original = original;
        SupportedArea = supportedArea;
        RetainedArea = retainedArea;
        Issue = issue;
    }

    /// <summary>Gets the original candidate photometric evidence.</summary>
    public Fs50RegionStatistics Original { get; }

    /// <summary>Gets locally supported ridge pixels.</summary>
    public int SupportedArea { get; }

    /// <summary>Gets retained pixels after supported expansion and contact intersection.</summary>
    public int RetainedArea { get; }

    /// <summary>Gets the candidate's capture rejection reason.</summary>
    public Fs50ContactIssue Issue { get; }
}
