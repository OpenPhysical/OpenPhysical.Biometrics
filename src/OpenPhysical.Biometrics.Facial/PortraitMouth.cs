// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Assesses lip closure from cached Google MediaPipe face landmarks.</summary>
public static class PortraitMouth
{
    private static readonly ImmutableArray<int> LipIndices = [78, 308, 13, 14, 82, 87, 312, 317];

    /// <summary>Measures three inner-lip separations without invoking a model or changing pixels.</summary>
    /// <param name="face">Immutable source-coordinate landmarks from the captured face.</param>
    /// <returns>Scale-independent measurements or an actionable unassessable finding.</returns>
    public static Result<PortraitMouthMeasurement, PortraitFinding> Measure(PortraitFace face)
    {
        var points = face.Landmarks;
        if (points.IsDefault || points.Length != 478 || LipIndices.Any(i => !Finite(points[i])))
            return Unassessable();
        var dx = points[308].X - points[78].X;
        var dy = points[308].Y - points[78].Y;
        var widthSquared = dx * dx + dy * dy;
        if (!double.IsFinite(widthSquared) || widthSquared <= 0)
            return Unassessable();
        double Gap(int upper, int lower) =>
            Math.Abs(
                dx * (points[lower].Y - points[upper].Y) - dy * (points[lower].X - points[upper].X)
            ) / widthSquared;
        var result = new PortraitMouthMeasurement(Gap(13, 14), Gap(82, 87), Gap(312, 317));
        return double.IsFinite(result.MaximumOpeningRatio) ? result : Unassessable();
    }

    /// <summary>Requires closed lips within the snapshotted application tolerance.</summary>
    /// <param name="face">Captured landmarks, without another neural-network evaluation.</param>
    /// <param name="policy">Validated application tolerance.</param>
    /// <returns>Success or a named finding that requires a retake or explicit operator override.</returns>
    public static UnitResult<PortraitFinding> Assess(PortraitFace face, PortraitMouthPolicy policy)
    {
        var measurement = Measure(face);
        return measurement.IsFailure ? measurement.Error : Assess(measurement.Value, policy);
    }

    /// <summary>Applies a fixed tolerance to an already measured captured mouth.</summary>
    /// <param name="measurement">The cached source-bound landmark measurement.</param>
    /// <param name="policy">Validated application tolerance.</param>
    /// <returns>Success or the named open-mouth retake finding.</returns>
    internal static UnitResult<PortraitFinding> Assess(
        PortraitMouthMeasurement measurement,
        PortraitMouthPolicy policy
    ) =>
        measurement.MaximumOpeningRatio <= policy.MaximumOpeningRatio
            ? UnitResult.Success<PortraitFinding>()
            : new PortraitFinding(
                "MouthOpen",
                "Retake the photograph with your lips gently closed."
            )
            {
                TestName = "Mouth closed",
                Action = PortraitFindingAction.Retake,
                CanOverride = true,
            };

    internal static UnitResult<PortraitFinding> Assess(
        Maybe<PortraitMouthMeasurement> measurement,
        PortraitMouthPolicy policy
    ) =>
        measurement.Match(value => Assess(value, policy), () => UnitResult.Failure(Unassessable()));

    private static bool Finite(FaceMeshPoint point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y);

    private static PortraitFinding Unassessable() =>
        new("MouthUnassessable", "Retake the photograph with your mouth clearly visible.")
        {
            TestName = "Mouth closed",
            Action = PortraitFindingAction.Retake,
            CanOverride = true,
        };
}
