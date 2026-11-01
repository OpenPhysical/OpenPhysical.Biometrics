// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>An immutable detection with the canonical MediaPipe 478-point topology.</summary>
/// <param name="Index">Stable zero-based index, ordered by horizontal center then vertical center.</param>
/// <param name="Confidence">BlazeFace detection probability.</param>
/// <param name="Presence">Landmark-network face-presence probability.</param>
/// <param name="Landmarks">The 478 mesh points in canonical model order.</param>
/// <param name="ViewerLeftEar">Detector estimate of the ear attachment on the viewer's left.</param>
/// <param name="ViewerRightEar">Detector estimate of the ear attachment on the viewer's right.</param>
public sealed record PortraitFace(
    int Index,
    double Confidence,
    double Presence,
    ImmutableArray<FaceMeshPoint> Landmarks,
    PortraitPoint ViewerLeftEar,
    PortraitPoint ViewerRightEar
)
{
    /// <summary>Gets anatomical features derived from the mesh topology, or none when the landmark set is invalid.</summary>
    public Maybe<PortraitFeatures> Features
    {
        get
        {
            var derived = PortraitFeatures.FromMediaPipe(Landmarks);
            return derived.IsSuccess ? Maybe.From(derived.Value) : Maybe<PortraitFeatures>.None;
        }
    }
}
