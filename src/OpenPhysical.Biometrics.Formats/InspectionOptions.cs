// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats;

/// <summary>Controls whether inspection reports include field contents.</summary>
/// <param name="IncludeSensitiveValues">Enables field values, including biometric and identity bytes, for explicitly requested inspection.</param>
public sealed record InspectionOptions(bool IncludeSensitiveValues = false);
