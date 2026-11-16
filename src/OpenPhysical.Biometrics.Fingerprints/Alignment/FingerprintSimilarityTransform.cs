// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

/// <summary>Maps reference to probe coordinates normalized to 197 pixels per centimetre.</summary>
/// <param name="A">Scale multiplied by the cosine of image-coordinate rotation.</param>
/// <param name="B">Scale multiplied by the sine of image-coordinate rotation.</param>
/// <param name="TranslationX">Horizontal translation at the normalized resolution.</param>
/// <param name="TranslationY">Vertical translation at the normalized resolution.</param>
public sealed record FingerprintSimilarityTransform(
    double A,
    double B,
    double TranslationX,
    double TranslationY
)
{
    /// <summary>Gets the dimensionless scale after physical-resolution normalization.</summary>
    public double Scale => Math.Sqrt(A * A + B * B);

    /// <summary>Gets clockwise rotation in degrees in the image coordinate system.</summary>
    public double RotationDegrees => Math.Atan2(B, A) * 180 / Math.PI;
}
