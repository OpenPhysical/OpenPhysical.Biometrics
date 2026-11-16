// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using OpenPhysical.Biometrics.NBIS;

/// <summary>Retains the exact image and its NFIQ 1 assessment without extracting minutiae.</summary>
public sealed class AssessedFingerprint
{
    internal AssessedFingerprint(FingerprintImage image, FingerprintQualityAssessment quality)
    {
        Image = image;
        Quality = quality;
    }

    /// <summary>Gets the original immutable image supplied to the quality provider.</summary>
    public FingerprintImage Image { get; }

    /// <summary>Gets the image's original five-level NFIQ 1 assessment.</summary>
    public FingerprintQualityAssessment Quality { get; }
}
