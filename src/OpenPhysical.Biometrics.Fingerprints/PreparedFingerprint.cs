// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using OpenPhysical.Biometrics.NBIS;

/// <summary>Retains one immutable image and its reusable extraction and image-quality results.</summary>
public sealed class PreparedFingerprint
{
    internal PreparedFingerprint(
        FingerprintImage image,
        FingerprintTemplate template,
        FingerprintQualityAssessment quality
    )
    {
        Image = image;
        Template = template;
        Quality = quality;
    }

    /// <summary>Gets the exact nominal 500 ppi image assessed and extracted.</summary>
    public FingerprintImage Image { get; }

    /// <summary>Gets the complete NBIS extraction before record-specific point selection.</summary>
    public FingerprintTemplate Template { get; }

    /// <summary>Gets the original parent image's NFIQ 1 assessment and algorithm identity.</summary>
    public FingerprintQualityAssessment Quality { get; }
}
