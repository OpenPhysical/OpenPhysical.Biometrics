// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints.Tests;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.NBIS;

internal sealed class TestQuality : IFingerprintQualityProvider
{
    private readonly int nfiq;

    internal TestQuality(int nfiq = 2)
    {
        this.nfiq = nfiq;
    }

    internal int Assessments { get; private set; }

    public Result<FingerprintQualityAssessment, FingerprintError> Assess(FingerprintImage image)
    {
        Assessments++;
        return FingerprintQualityAssessment.Create(nfiq, "test-provider");
    }
}
