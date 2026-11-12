// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

/// <summary>The deterministic reason for choosing a frozen acquisition.</summary>
public enum CaptureSelectionReason
{
    /// <summary>Best motion-ranked eligible acquisition.</summary>
    MotionBaseline,

    /// <summary>A meaningful non-dominated quality improvement.</summary>
    QualityImprovement,

    /// <summary>The operator explicitly chose this image.</summary>
    Manual,
}
