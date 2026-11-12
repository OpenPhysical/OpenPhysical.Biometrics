// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using OpenPhysical.Biometrics.Fingerprints;

/// <summary>An actual finger view in a loaded reference document.</summary>
/// <param name="Index">Actual index in the loaded record.</param>
/// <param name="Finger">Actual ANSI finger identity.</param>
/// <param name="ViewNumber">Actual view number within that finger.</param>
public sealed record ReferenceViewChoice(int Index, FingerPosition Finger, byte ViewNumber)
{
    internal static ReferenceViewChoice Empty { get; } = new(-1, FingerPosition.RightIndex, 0);

    /// <summary>Gets the operator-visible finger and view label.</summary>
    public string Label =>
        Index < 0 ? string.Empty : $"{FingerChoice.LabelFor(Finger)} · view {ViewNumber}";

    /// <inheritdoc/>
    public override string ToString() => Label;
}
