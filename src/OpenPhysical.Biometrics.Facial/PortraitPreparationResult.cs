// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>An owned portrait preparation result with immutable pixels and findings.</summary>
/// <param name="Status">Preparation outcome.</param>
/// <param name="SourceSha256">Identity of the encoded source or geometry-bound camera RGB input.</param>
/// <param name="SourceWidth">Oriented source width.</param>
/// <param name="SourceHeight">Oriented source height.</param>
/// <param name="Faces">Detections in stable order.</param>
/// <param name="Transform">Source-to-inspection-portrait mapping when supported by the source.</param>
/// <param name="Measurements">Framing measurements when an inspection portrait is available.</param>
/// <param name="RgbPixels">Owned 480 by 640 sRGB inspection pixels; availability does not imply acceptance.</param>
/// <param name="Findings">Structural and review findings.</param>
public sealed record PortraitPreparationResult(
    PortraitStatus Status,
    string SourceSha256,
    int SourceWidth,
    int SourceHeight,
    ImmutableArray<PortraitFace> Faces,
    Maybe<PortraitTransform> Transform,
    Maybe<PortraitMeasurements> Measurements,
    ImmutableArray<byte> RgbPixels,
    ImmutableArray<PortraitFinding> Findings
)
{
    /// <summary>Gets the immutable mouth tolerance used for this exact preparation.</summary>
    public PortraitMouthPolicy MouthPolicy { get; init; } = PortraitMouthPolicy.Default;

    /// <summary>Gets the source-bound mouth measurement, absent when it was unassessable.</summary>
    public Maybe<PortraitMouthMeasurement> MouthMeasurement { get; init; }

    /// <summary>Gets explicit background-editing evidence, separate from acquisition and pose findings.</summary>
    public Maybe<PortraitBackgroundEvidence> Background { get; init; }

    /// <summary>Gets the pinned model source revision used by this implementation.</summary>
    public string ModelRevision => PortraitProvenance.ModelRevision;

    /// <summary>Gets the versioned crop policy used by this implementation.</summary>
    public string GeometryPolicy => PortraitProvenance.GeometryPolicy;
}
