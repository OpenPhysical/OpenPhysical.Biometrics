// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Reliable original-stage image area and retained diagnostics, without a liveness assertion.</summary>
public sealed class Fs50ContactRegion
{
    internal Fs50ContactRegion(Fs50ContactDiagnostics diagnostics) => Diagnostics = diagnostics;

    /// <summary>Gets immutable evidence for every image-processing stage and rejected candidate.</summary>
    public Fs50ContactDiagnostics Diagnostics { get; }

    /// <summary>Gets the retained binary image mask, one included and zero excluded.</summary>
    public ImmutableArray<byte> Mask => Diagnostics.AcceptedMask;

    /// <summary>Gets one selected connected texture component, without claiming every pixel has reliable ridges.</summary>
    public ImmutableArray<byte> ContactMask => Diagnostics.ContactMask;

    /// <summary>Gets retained supported image area.</summary>
    public int Area => Diagnostics.AcceptedArea;

    /// <summary>Gets substantial accepted component count.</summary>
    public int ComponentCount => Diagnostics.ComponentCount;

    /// <summary>Gets coherent spatial candidates before the original-image darkness check.</summary>
    public int SpatialComponentCount => Diagnostics.SpatialComponentCount;

    /// <summary>Gets the selected original weak-component statistics without tone adjustment.</summary>
    public Maybe<Fs50RegionStatistics> SpatialStatistics => Diagnostics.SpatialStatistics;

    /// <summary>Gets why observed candidates were rejected.</summary>
    public Fs50ContactIssue Issue => Diagnostics.Issue;

    /// <summary>Gets whether usable ridge contact was observed.</summary>
    public bool Present => Area > 0;

    /// <summary>Gets retained-area percentile contrast.</summary>
    public double Contrast => Diagnostics.AcceptedStatistics.Contrast;

    /// <summary>Gets retained-area inverted fifth-percentile intensity for exposure control.</summary>
    public double Signal => Diagnostics.AcceptedStatistics.Signal;

    /// <summary>Copies the exact retained and guarded planes without recomputing evidence.</summary>
    /// <returns>Validated processing masks in unchanged original coordinates.</returns>
    public Result<FingerprintProcessingMask, CaptureError> ToProcessingMask() =>
        FingerprintProcessingMask.Create(
            800,
            750,
            Diagnostics.AcceptedMask.AsSpan(),
            Diagnostics.InnerMask.AsSpan()
        );
}
