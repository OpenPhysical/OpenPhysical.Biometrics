// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>An owned portrait preparation result with immutable pixels and findings.</summary>
/// <param name="Status">Preparation outcome.</param>
/// <param name="SourceSha256">Hash of the encoded source.</param>
/// <param name="SourceWidth">Oriented source width.</param>
/// <param name="SourceHeight">Oriented source height.</param>
/// <param name="Faces">Detections in stable order.</param>
/// <param name="Transform">Source-to-output mapping when prepared.</param>
/// <param name="Measurements">Crop measurements when prepared.</param>
/// <param name="RgbPixels">Owned 480 by 640 sRGB pixels, or empty when review is required.</param>
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
    /// <summary>Gets the pinned model source revision used by this implementation.</summary>
    public string ModelRevision => PortraitProvenance.ModelRevision;

    /// <summary>Gets the versioned crop policy used by this implementation.</summary>
    public string GeometryPolicy => PortraitProvenance.GeometryPolicy;
}
