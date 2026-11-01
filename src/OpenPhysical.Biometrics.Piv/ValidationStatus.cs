// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

/// <summary>Records the outcome of one independently evaluated check.</summary>
public enum ValidationStatus
{
    /// <summary>The check awaits evaluation by the responsible service.</summary>
    NotEvaluated,

    /// <summary>The check passed.</summary>
    Passed,

    /// <summary>The check failed.</summary>
    Failed,
}
