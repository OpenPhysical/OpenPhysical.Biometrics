// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Biometrics.Facial;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>A line in the same coordinate system as the displayed image.</summary>
/// <param name="Start">First endpoint.</param>
/// <param name="End">Second endpoint.</param>
/// <param name="Tone">Inspection meaning.</param>
public sealed record FaceGuideSegment(PortraitPoint Start, PortraitPoint End, FaceGuideTone Tone)
{
    /// <summary>Gets whether the line marks a range rather than a measured position.</summary>
    public bool IsDashed { get; init; }
}
