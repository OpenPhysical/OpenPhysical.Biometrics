// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Application-owned display name for a shared Studio shell.</summary>
/// <param name="Name">Operator-facing application and window title.</param>
/// <param name="InitialWorkspaceIndex">Initial tab, with Capture zero, Compare one, then trusted tasks.</param>
public sealed record DesktopApplicationIdentity(string Name, int InitialWorkspaceIndex = 0);
