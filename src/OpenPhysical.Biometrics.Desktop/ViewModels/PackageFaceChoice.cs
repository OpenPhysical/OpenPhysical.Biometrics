// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

/// <summary>An actual image block in a retained facial record.</summary>
/// <param name="Index">Zero-based image index.</param>
/// <param name="Width">Recorded pixel width.</param>
/// <param name="Height">Recorded pixel height.</param>
public sealed record PackageFaceChoice(int Index, int Width, int Height)
{
    /// <summary>Gets a compact image label.</summary>
    public string Label => $"Portrait {Index + 1} · {Width} × {Height}";

    /// <inheritdoc/>
    public override string ToString() => Label;
}
