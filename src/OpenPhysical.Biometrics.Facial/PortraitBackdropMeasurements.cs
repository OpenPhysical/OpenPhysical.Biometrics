// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using OpenPhysical.Biometrics.Imaging;

/// <summary>Conservative backdrop evidence, never a PIV compliance decision.</summary>
/// <param name="EstimatedCandidate">Whether sufficient protected border-connected pixels were measured.</param>
/// <param name="CandidateFraction">Candidate area divided by the prepared canvas area.</param>
/// <param name="ColorConsistency">Consistency relative to robust border color.</param>
/// <param name="Statistics">Native candidate RGB evidence; absent channels mean no candidate samples.</param>
/// <param name="MaskSha256">Digest of the binary candidate mask.</param>
/// <param name="Policy">Parameters used for the estimate.</param>
/// <param name="Face">Statistics of protected facial pixels.</param>
/// <param name="ProtectedForeground">Statistics of protected head and shoulder pixels.</param>
/// <param name="Unclassified">Statistics of pixels without backdrop evidence.</param>
public sealed record PortraitBackdropMeasurements(
    bool EstimatedCandidate,
    double CandidateFraction,
    double ColorConsistency,
    RgbRegionStatistics Statistics,
    string MaskSha256,
    PortraitBackdropOptions Policy,
    RgbRegionStatistics Face,
    RgbRegionStatistics ProtectedForeground,
    RgbRegionStatistics Unclassified
);
