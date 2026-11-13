// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

/// <summary>An application failure with a stable code and operator-facing explanation.</summary>
/// <param name="Code">Stable machine-readable failure identifier.</param>
/// <param name="Message">Explanation suitable for the operator.</param>
public sealed record DesktopError(string Code, string Message);
