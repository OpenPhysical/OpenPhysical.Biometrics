// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Piv;

/// <summary>Rejects excessive source pose before any portrait correction.</summary>
/// <remarks>Angles are image-based estimates; attended frontal-pose review remains required.</remarks>
public static class PortraitSourcePose
{
    // Avoid admitting the exact excluded boundary through floating-point roundoff.
    private const double BoundaryGuardDegrees = 1e-9;

    /// <summary>Checks cached source pose against the Full Frontal limit.</summary>
    /// <param name="face">Source-bound detection with a cached canonical pose estimate.</param>
    /// <param name="features">Anatomical features in the same uncorrected source coordinates.</param>
    /// <returns>Success or an actionable retake finding; unknown pose does not pass.</returns>
    public static UnitResult<PortraitFinding> Validate(PortraitFace face, PortraitFeatures features)
    {
        var findings = Findings(face, features);
        return findings.IsEmpty
            ? UnitResult.Success<PortraitFinding>()
            : UnitResult.Failure(findings[0]);
    }

    /// <summary>Lists every failed source-pose check without changing the image.</summary>
    /// <param name="face">Source-bound detection with cached pose.</param>
    /// <param name="features">Anatomical features in the original source.</param>
    /// <returns>All failed pose checks, or an empty collection when they pass.</returns>
    public static ImmutableArray<PortraitFinding> Findings(
        PortraitFace face,
        PortraitFeatures features
    )
    {
        if (!ValidEyes(features) || face.Pose.HasNoValue || !Finite(face.Pose.Value))
            return [Unassessable()];
        var pose = face.Pose.Value;
        var findings = ImmutableArray.CreateBuilder<PortraitFinding>();
        if (Excessive(pose.YawDegrees))
            findings.Add(
                Finding(
                    "SourceYaw",
                    "Head turn",
                    "Retake the photograph looking straight at the camera, without turning your head."
                )
            );
        if (Excessive(pose.PitchDegrees))
            findings.Add(
                Finding(
                    "SourcePitch",
                    "Head up/down angle",
                    "Retake the photograph with the camera at eye level. Look straight ahead without looking up or down."
                )
            );
        if (Excessive(pose.RollDegrees))
            findings.Add(
                Finding(
                    "SourceRoll",
                    "Head tilt",
                    "Retake the photograph with your head upright and facing the camera."
                )
            );
        return findings.ToImmutable();
    }

    private static bool ValidEyes(PortraitFeatures features)
    {
        var dx = features.ViewerRightEye.X - features.ViewerLeftEye.X;
        var dy = features.ViewerRightEye.Y - features.ViewerLeftEye.Y;
        return double.IsFinite(dx) && double.IsFinite(dy) && (dx != 0 || dy != 0);
    }

    private static PortraitFinding Unassessable() =>
        Finding(
            "SourcePoseUnassessable",
            "Face pose",
            "Retake the photograph with both eyes clearly visible and face the camera."
        );

    private static bool Excessive(double degrees) =>
        Math.Abs(degrees) + BoundaryGuardDegrees >= PivPortraitGeometry.MaximumPoseDeviationDegrees;

    private static bool Finite(PortraitPoseEstimate pose) =>
        double.IsFinite(pose.YawDegrees)
        && double.IsFinite(pose.PitchDegrees)
        && double.IsFinite(pose.RollDegrees)
        && double.IsFinite(pose.VerticalFieldOfViewDegrees)
        && pose.VerticalFieldOfViewDegrees is > 0 and < 180;

    private static PortraitFinding Finding(string code, string name, string message) =>
        new(code, message)
        {
            Action = PortraitFindingAction.Retake,
            TestName = name,
            CanOverride = true,
        };
}
