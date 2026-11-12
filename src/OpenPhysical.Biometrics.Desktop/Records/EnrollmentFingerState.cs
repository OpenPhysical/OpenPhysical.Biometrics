// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

/// <summary>The operator-visible state of a required finger.</summary>
public enum EnrollmentFingerState
{
    /// <summary>No usable capture is waiting for acceptance.</summary>
    WaitingForCapture,

    /// <summary>A capture is ready for explicit acceptance.</summary>
    WaitingForAcceptance,

    /// <summary>The operator accepted this finger.</summary>
    Accepted,
}
