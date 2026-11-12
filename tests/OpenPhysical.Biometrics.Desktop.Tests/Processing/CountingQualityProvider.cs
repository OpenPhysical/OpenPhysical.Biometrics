// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

internal sealed class CountingQualityProvider(IFingerprintQualityProvider inner)
    : IFingerprintQualityProvider
{
    internal int Calls { get; private set; }

    public Result<FingerprintQualityAssessment, FingerprintError> Assess(FingerprintImage image)
    {
        Calls++;
        return inner.Assess(image);
    }
}
