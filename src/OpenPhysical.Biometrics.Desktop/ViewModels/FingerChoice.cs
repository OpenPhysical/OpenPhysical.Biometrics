// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>A finger identity and its operator-facing label.</summary>
/// <param name="Position">Standard finger identity.</param>
/// <param name="Label">Operator-facing finger label.</param>
public sealed record FingerChoice(FingerPosition Position, string Label)
{
    /// <summary>Gets all standard finger choices.</summary>
    public static ImmutableArray<FingerChoice> All { get; } =
        Enum.GetValues<FingerPosition>()
            .Select(position => new FingerChoice(position, LabelFor(position)))
            .ToImmutableArray();

    /// <inheritdoc/>
    public override string ToString() => Label;

    /// <summary>Finds the choice for a defined standard finger.</summary>
    /// <param name="finger">Defined standard finger identity.</param>
    /// <returns>The corresponding choice.</returns>
    public static FingerChoice For(FingerPosition finger) =>
        All.Single(choice => choice.Position == finger);

    /// <summary>Formats a standard finger identity for display.</summary>
    /// <param name="position">Standard finger identity.</param>
    /// <returns>Spaced finger label.</returns>
    public static string LabelFor(FingerPosition position) =>
        string.Concat(
            position
                .ToString()
                .Select(
                    (character, index) =>
                        index > 0 && char.IsUpper(character)
                            ? " " + character
                            : character.ToString()
                )
        );
}
