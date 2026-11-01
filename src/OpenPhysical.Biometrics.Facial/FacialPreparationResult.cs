// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Piv;

/// <summary>Contains a prepared facial record and its acquisition evidence, or a review outcome.</summary>
/// <param name="Status">The preparation outcome.</param>
/// <param name="Record">The canonical facial record when prepared.</param>
/// <param name="Acquisition">The supplied camera/operator evidence.</param>
/// <param name="ProfileDiagnostics">Specific metadata findings.</param>
public sealed record FacialPreparationResult(
    FacialPreparationStatus Status,
    Maybe<FaceRecord> Record,
    FacialAcquisitionMetadata Acquisition,
    ImmutableArray<BiometricProfileDiagnostic> ProfileDiagnostics
);
