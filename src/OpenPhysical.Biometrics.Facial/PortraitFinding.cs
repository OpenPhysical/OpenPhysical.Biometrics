// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

/// <summary>A stable diagnostic for portrait preparation.</summary>
/// <param name="Code">Machine-readable diagnostic code.</param>
/// <param name="Message">Plain-language explanation.</param>
public sealed record PortraitFinding(string Code, string Message)
{
    /// <summary>Gets whether the capture can be reviewed or requires a new acquisition.</summary>
    public PortraitFindingAction Action { get; init; }

    /// <summary>Gets the human-readable check name used in capture feedback.</summary>
    public string TestName { get; init; } = string.Empty;

    /// <summary>Gets whether an explicit unsigned-staging override may acknowledge this check.</summary>
    public bool CanOverride { get; init; }
}
