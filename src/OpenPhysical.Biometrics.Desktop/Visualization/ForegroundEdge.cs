// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

/// <summary>A source-pixel edge in a displayed region outline.</summary>
/// <param name="X1">Start column.</param>
/// <param name="Y1">Start row.</param>
/// <param name="X2">End column.</param>
/// <param name="Y2">End row.</param>
public readonly record struct ForegroundEdge(int X1, int Y1, int X2, int Y2);
