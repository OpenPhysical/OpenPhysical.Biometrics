// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Workstation.Application.Verification;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>An enrolled finger authenticated by the current card check.</summary>
/// <param name="Choice">Exact card-authenticated finger and view.</param>
public sealed record StudioFingerChoice(GuidedFingerChoice Choice)
{
    /// <summary>Human-readable enrolled-finger instruction.</summary>
    public string Label =>
        Choice.Finger switch
        {
            1 => "Right thumb",
            2 => "Right index finger",
            3 => "Right middle finger",
            4 => "Right ring finger",
            5 => "Right little finger",
            6 => "Left thumb",
            7 => "Left index finger",
            8 => "Left middle finger",
            9 => "Left ring finger",
            10 => "Left little finger",
            _ => "Enrolled finger",
        };

    /// <summary>Displays the finger instruction.</summary>
    /// <returns>The human-readable selection label.</returns>
    public override string ToString() => Label;
}
