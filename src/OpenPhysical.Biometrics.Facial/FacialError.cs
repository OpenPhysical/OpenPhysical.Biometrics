// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>Describes a failed facial operation without exposing image or identity contents.</summary>
/// <param name="Code">Stable, machine-readable error identifier; see <see cref="FacialErrorCodes"/>.</param>
/// <param name="Message">Explanation of the failed check.</param>
public sealed record FacialError(string Code, string Message);
