// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Application choices for contact evidence; validate through <see cref="Fs50EvidencePolicy.Create"/>.</summary>
public sealed record Fs50EvidenceOptions
{
    /// <summary>Gets minimum primary local texture variance.</summary>
    public double TextureVariance { get; init; } = 90;

    /// <summary>Gets the primary local mean upper bound.</summary>
    public double TextureMean { get; init; } = 242;

    /// <summary>Gets minimum faint-contact local variance.</summary>
    public double FaintVariance { get; init; } = 9;

    /// <summary>Gets the faint-contact local mean upper bound.</summary>
    public double FaintMean { get; init; } = 252;

    /// <summary>Gets connected candidate minimum area.</summary>
    public int MinimumArea { get; init; } = 4000;

    /// <summary>Gets connected candidate minimum width.</summary>
    public int MinimumWidth { get; init; } = 40;

    /// <summary>Gets connected candidate minimum height.</summary>
    public int MinimumHeight { get; init; } = 80;

    /// <summary>Gets minimum inverted fifth-percentile intensity.</summary>
    public double MinimumSignal { get; init; } = 50;

    /// <summary>Gets candidate closing radius.</summary>
    public int ClosingRadius { get; init; } = 6;

    /// <summary>Gets candidate opening radius.</summary>
    public int OpeningRadius { get; init; } = 3;

    /// <summary>Gets minimum local orientation coherence.</summary>
    public double MinimumCoherence { get; init; } = 0.35;

    /// <summary>Gets minimum occupied fraction of a support window.</summary>
    public double MinimumBlockCoverage { get; init; } = 0.75;

    /// <summary>Gets minimum cross-ridge profile span.</summary>
    public double MinimumProfileContrast { get; init; } = 12;

    /// <summary>Gets minimum ridge/valley transitions.</summary>
    public int MinimumTransitions { get; init; } = 4;

    /// <summary>Gets minimum ridge period in pixels.</summary>
    public int MinimumPeriod { get; init; } = 6;

    /// <summary>Gets maximum ridge period in pixels.</summary>
    public int MaximumPeriod { get; init; } = 16;

    /// <summary>Gets minimum full-period correlation.</summary>
    public double MinimumPeriodCorrelation { get; init; } = 0.55;

    /// <summary>Gets maximum half-period correlation.</summary>
    public double MaximumHalfPeriodCorrelation { get; init; } = -0.1;

    /// <summary>Gets minimum agreement along the ridge direction.</summary>
    public double MinimumParallelCorrelation { get; init; } = 0.55;

    /// <summary>Gets allowed parallel-profile alignment displacement.</summary>
    public int MaximumParallelShift { get; init; } = 3;

    /// <summary>Gets minimum neighboring ridge-supported blocks.</summary>
    public int MinimumSupportedBlocks { get; init; } = 3;

    /// <summary>Gets allowed neighboring ridge-period difference.</summary>
    public int PeriodTolerance { get; init; } = 2;

    /// <summary>Gets dilation of supported neighborhoods before contact intersection.</summary>
    public int SupportedExpansion { get; init; } = 16;

    /// <summary>Gets the ridge-support reach used only to reject unsupported exterior.</summary>
    public int RidgeBoundaryTolerance { get; init; } = 16;

    /// <summary>Gets the inner-area guard applied when retaining minutiae.</summary>
    public int InnerGuard { get; init; } = 8;
}
