// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>A stable diagnostic for portrait preparation.</summary>
/// <param name="Code">Machine-readable diagnostic code.</param>
/// <param name="Message">Plain-language explanation.</param>
public sealed record PortraitFinding(string Code, string Message);
