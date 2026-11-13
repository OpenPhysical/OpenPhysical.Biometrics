// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

internal enum FaceReadinessState
{
    Ready,
    FaceCount,
    Landmarks,
    Pose,
    Mouth,
    Framing,
}

internal sealed record FaceCaptureReadiness(FaceReadinessState State, string Guidance)
{
    internal bool Ready => State == FaceReadinessState.Ready;
}
