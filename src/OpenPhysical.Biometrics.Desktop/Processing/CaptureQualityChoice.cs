// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Indices and the policy reason for the selected frozen acquisition.</summary>
/// <param name="SelectedIndex">Index of the selected candidate.</param>
/// <param name="BaselineIndex">Index of the motion-ranked usable baseline.</param>
/// <param name="Reason">Policy reason for the selection.</param>
public sealed record CaptureQualityChoice(
    int SelectedIndex,
    int BaselineIndex,
    CaptureSelectionReason Reason
);
