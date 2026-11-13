// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal sealed record PortraitCaptureAssessment(
    ImmutableArray<PortraitFinding> Findings,
    Maybe<PortraitMouthMeasurement> MouthMeasurement
);
