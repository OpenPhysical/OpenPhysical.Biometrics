// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

/// <summary>Describes the selected finger and view metadata.</summary>
/// <param name="FingerPosition">Encoded finger position.</param>
/// <param name="ViewNumber">Encoded view selection.</param>
/// <param name="MinutiaCount">Number of encoded minutiae.</param>
public sealed record FingerViewSummary(byte FingerPosition, byte ViewNumber, int MinutiaCount);
