// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Formats;

/// <summary>Provides shared inspection data for CLI, middleware, and application renderers.</summary>
/// <param name="Kind">The identified record kind.</param>
/// <param name="Structure">Located structural fields and diagnostics.</param>
/// <param name="ProfileDiagnostics">PIV profile findings, independent of external cryptographic findings.</param>
/// <param name="FingerViews">Selections and counts for fingerprint records.</param>
/// <param name="FacialImages">Geometry and encoding for facial records.</param>
/// <param name="HasSignatureBlock">Whether the CBEFF object contains a detached security block.</param>
public sealed record BiometricInspectionReport(
    BiometricRecordKind Kind,
    InspectionReport Structure,
    ImmutableArray<BiometricProfileDiagnostic> ProfileDiagnostics,
    ImmutableArray<FingerViewSummary> FingerViews,
    ImmutableArray<FacialImageSummary> FacialImages,
    bool HasSignatureBlock
);
