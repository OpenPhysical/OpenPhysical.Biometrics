// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

/// <summary>Describes the image condition reported by an NFIQ 1 assessment.</summary>
public enum FingerprintQualityStatus
{
    /// <summary>The classifier assessed the image normally.</summary>
    Assessed,

    /// <summary>The image contains no usable fingerprint foreground.</summary>
    EmptyImage,

    /// <summary>The image contains too few detected minutiae for normal classification.</summary>
    InsufficientMinutiae,
}

/// <summary>A validated NFIQ 1 assessment with its algorithm identity.</summary>
public sealed class FingerprintQualityAssessment
{
    /// <summary>Creates an assessment on the original five-level NFIQ 1 scale.</summary>
    /// <param name="nfiq1">NFIQ 1 classification, from 1 (best) through 5 (poorest).</param>
    /// <param name="algorithmVersion">The quality provider's version or pinned model identity.</param>
    /// <param name="confidence">Optional classifier confidence, from zero through one.</param>
    /// <param name="status">The image condition returned by the quality engine.</param>
    /// <exception cref="ArgumentOutOfRangeException">The classification or confidence is invalid.</exception>
    /// <exception cref="ArgumentException">Algorithm identity is blank.</exception>
    public FingerprintQualityAssessment(
        int nfiq1,
        string algorithmVersion,
        double? confidence = null,
        FingerprintQualityStatus status = FingerprintQualityStatus.Assessed
    )
    {
        if (nfiq1 is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(nfiq1));
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithmVersion);
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (confidence is { } value && (!double.IsFinite(value) || value is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(confidence));
        Nfiq1 = nfiq1;
        AlgorithmVersion = algorithmVersion;
        Confidence = confidence;
        Status = status;
    }

    /// <summary>Gets the NFIQ 1 class, from 1 through 5.</summary>
    public int Nfiq1 { get; }

    /// <summary>Gets the PIV quality encoding, calculated as 20 × (6 − NFIQ 1).</summary>
    public byte PivQuality => checked((byte)(20 * (6 - Nfiq1)));

    /// <summary>Gets the algorithm version or pinned model identity.</summary>
    public string AlgorithmVersion { get; }

    /// <summary>Gets classifier confidence when supplied by the provider.</summary>
    public double? Confidence { get; }

    /// <summary>Gets the image condition reported with the quality class.</summary>
    public FingerprintQualityStatus Status { get; }
}

/// <summary>Supplies the NFIQ 1 assessment used in PIV enrollment.</summary>
public interface IFingerprintQualityProvider
{
    /// <summary>Assesses a captured image on the original NFIQ 1 scale.</summary>
    /// <param name="capture">The immutable capture to assess.</param>
    /// <returns>The NFIQ 1 class and provider identity.</returns>
    FingerprintQualityAssessment Assess(FingerprintCapture capture);
}
