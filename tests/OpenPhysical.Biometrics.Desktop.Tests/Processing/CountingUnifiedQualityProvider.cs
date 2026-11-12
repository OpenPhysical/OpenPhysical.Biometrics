// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

namespace OpenPhysical.Biometrics.Desktop.Tests;

internal sealed class CountingUnifiedQualityProvider(IFingerprintUnifiedQualityProvider inner)
    : IFingerprintUnifiedQualityProvider
{
    internal int Calls { get; private set; }
    internal Maybe<FingerprintImage> Image { get; private set; }

    public Result<FingerprintUnifiedQualityAssessment, FingerprintError> Assess(
        FingerprintImage image
    )
    {
        Calls++;
        Image = image;
        return inner.Assess(image);
    }
}
