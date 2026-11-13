// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using OpenPhysical.Biometrics.Facial;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Immutable image annotations derived from one cached facial analysis or crop plan.</summary>
/// <param name="SourceSha256">Original source identity, never a compliance assertion.</param>
/// <param name="SourceCoordinates">Whether positions refer to the acquisition rather than prepared portrait.</param>
/// <param name="Segments">Framing and measurement lines.</param>
/// <param name="DetectionPoints">Optional Google mesh evidence.</param>
public sealed record FaceImageGuides(
    string SourceSha256,
    bool SourceCoordinates,
    ImmutableArray<FaceGuideSegment> Segments,
    ImmutableArray<PortraitPoint> DetectionPoints
);
