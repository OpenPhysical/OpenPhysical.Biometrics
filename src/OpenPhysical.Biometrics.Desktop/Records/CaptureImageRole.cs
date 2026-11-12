// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

/// <summary>The assessed image role, bound to its exact digest.</summary>
public enum CaptureImageRole
{
    /// <summary>Unmodified source acquisition.</summary>
    Original,

    /// <summary>Exact prepared processing pixels.</summary>
    Processed,
}
