// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using CSharpFunctionalExtensions;

internal sealed class TestQuality : IFingerprintQualityProvider
{
    public Result<FingerprintQualityAssessment, FingerprintError> Assess(
        FingerprintCapture capture
    ) => FingerprintQualityAssessment.Create(2, "test-provider");
}
