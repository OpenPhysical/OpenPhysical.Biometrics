// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>A canonical-face pose estimate, not a calibrated physical compliance measurement.</summary>
/// <param name="PitchDegrees">Estimated up/down rotation; positive looks down.</param>
/// <param name="YawDegrees">Estimated turn; positive looks toward the subject's left.</param>
/// <param name="RollDegrees">Estimated tilt; positive tilts toward the subject's right.</param>
/// <param name="VerticalFieldOfViewDegrees">Virtual perspective-camera field of view used by the solve.</param>
public sealed record PortraitPoseEstimate(
    double PitchDegrees,
    double YawDegrees,
    double RollDegrees,
    double VerticalFieldOfViewDegrees
)
{
    /// <summary>Gets the versioned geometric estimator.</summary>
    public string PolicyId => "google-canonical-pose-v1";

    /// <summary>Gets the pinned canonical geometry identity used by the estimator.</summary>
    public string CanonicalModelSha256 => PortraitPoseEstimator.CanonicalModelSha256;
}
