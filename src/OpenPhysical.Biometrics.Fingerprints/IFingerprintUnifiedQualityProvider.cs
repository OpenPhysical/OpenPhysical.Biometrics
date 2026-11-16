// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.NBIS;

/// <summary>Supplies supplemental continuous image quality independently of NFIQ 1 and PIV.</summary>
public interface IFingerprintUnifiedQualityProvider
{
    /// <summary>Assesses the original image without changing its pixels or metadata.</summary>
    /// <param name="image">The immutable nominal 500 ppi grayscale image.</param>
    /// <returns>The score and algorithm identity, or a precise assessment failure.</returns>
    Result<FingerprintUnifiedQualityAssessment, FingerprintError> Assess(FingerprintImage image);
}
