// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Biometrics.Capture.Camera;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Operator evidence for a 20 cm optical target at a distance of 1.5 m.</summary>
/// <param name="SourceId">Actual platform camera identity.</param>
/// <param name="Mode">Actual native capture mode.</param>
/// <param name="TargetPixels">Measured target width without digital enlargement.</param>
/// <param name="ConfirmedAt">Time the operator recorded the measurement.</param>
/// <param name="OperatorId">Operator-supplied acquisition identifier.</param>
public sealed record CameraOpticalQualification(
    string SourceId,
    CameraMode Mode,
    double TargetPixels,
    DateTimeOffset ConfirmedAt,
    string OperatorId
)
{
    internal static bool Matches(
        CameraOpticalQualification value,
        string sourceId,
        CameraMode mode
    ) => Valid(value) && value.SourceId == sourceId && value.Mode == mode;

    internal static bool Valid(CameraOpticalQualification value) =>
        value is not null
        && !string.IsNullOrWhiteSpace(value.SourceId)
        && !string.IsNullOrWhiteSpace(value.OperatorId)
        && value.OperatorId.Length <= 128
        && !value.OperatorId.Any(char.IsControl)
        && value.TargetPixels >= 240
        && double.IsFinite(value.TargetPixels)
        && value.Mode is not null
        && value.TargetPixels <= value.Mode.Width
        && value.Mode.Width > 420
        && value.Mode.Height > 0
        && value.ConfirmedAt != default
        && value.ConfirmedAt.Offset == TimeSpan.Zero;
}
