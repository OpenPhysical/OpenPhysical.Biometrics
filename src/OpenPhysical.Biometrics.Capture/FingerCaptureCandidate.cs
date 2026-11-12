// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using System.Collections.Immutable;

/// <summary>An immutable previously displayed candidate with its original acquisition evidence.</summary>
/// <param name="Frame">Exact original pixels, timestamp and provenance.</param>
/// <param name="ForegroundMask">Foreground measured from that frame.</param>
/// <param name="Motion">Brightness-normalized, centroid-compensated motion; infinity until compared.</param>
/// <param name="ClippedFraction">Fraction of foreground samples at black or white saturation.</param>
/// <param name="ForegroundArea">Foreground pixel count.</param>
/// <param name="ObservedAt">Monotonic timestamp when the image was acknowledged.</param>
public sealed record FingerCaptureCandidate(
    CaptureFrame Frame,
    ImmutableArray<byte> ForegroundMask,
    double Motion,
    double ClippedFraction,
    int ForegroundArea,
    long ObservedAt
);
