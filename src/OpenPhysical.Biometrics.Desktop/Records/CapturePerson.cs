// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

/// <summary>The explicit local person label associated with a saved capture session.</summary>
/// <param name="DisplayName">Operator-provided name or alias.</param>
/// <param name="SubjectIdentifier">Optional local identifier for this person.</param>
public sealed record CapturePerson(string DisplayName, string SubjectIdentifier)
{
    /// <summary>Validates an explicitly supplied name or alias and optional local identifier.</summary>
    /// <param name="name">Person label, up to 128 characters.</param>
    /// <param name="identifier">Optional local identifier, up to 256 characters.</param>
    /// <returns>The validated label or a precise input failure.</returns>
    public static Result<CapturePerson, DesktopError> Create(string name, string identifier)
    {
        if (string.IsNullOrWhiteSpace(name) || identifier is null)
            return new DesktopError(
                "Session.Person",
                "Enter a name or label for this capture session."
            );
        var label = name.Trim();
        var subject = identifier.Trim();
        return
            label.Length is >= 1 and <= 128
            && subject.Length <= 256
            && !label.Any(char.IsControl)
            && !subject.Any(char.IsControl)
            ? new CapturePerson(label, subject)
            : new DesktopError(
                "Session.Person",
                "Enter a name or label of up to 128 characters. The optional identifier can contain up to 256 characters."
            );
    }
}
