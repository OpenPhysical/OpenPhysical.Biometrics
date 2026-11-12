// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Controls optional diagnostic image companions for a Studio capture package.</summary>
/// <param name="IncludeDiagnostics">Includes the five additional contact-analysis planes when true.</param>
public sealed record TemplateSaveOptions(bool IncludeDiagnostics)
{
    /// <summary>Gets normal evidence export without additional diagnostic planes.</summary>
    public static TemplateSaveOptions Default { get; } = new(false);
}
