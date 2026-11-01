// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;

/// <summary>A validated NFIQ 1 assessment with its algorithm identity.</summary>
public sealed class FingerprintQualityAssessment
{
    private FingerprintQualityAssessment(
        int nfiq1,
        string algorithmVersion,
        Maybe<double> confidence,
        FingerprintQualityStatus status
    )
    {
        Nfiq1 = nfiq1;
        AlgorithmVersion = algorithmVersion;
        Confidence = confidence;
        Status = status;
    }

    /// <summary>Gets the NFIQ 1 class, from 1 through 5.</summary>
    public int Nfiq1 { get; }

    /// <summary>Gets the PIV quality encoding, calculated as 20 × (6 − NFIQ 1).</summary>
    public byte PivQuality => (byte)(20 * (6 - Nfiq1));

    /// <summary>Gets the algorithm version or pinned model identity.</summary>
    public string AlgorithmVersion { get; }

    /// <summary>Gets classifier confidence when supplied by the provider.</summary>
    public Maybe<double> Confidence { get; }

    /// <summary>Gets the image condition reported with the quality class.</summary>
    public FingerprintQualityStatus Status { get; }

    /// <summary>Creates an assessment on the original five-level NFIQ 1 scale.</summary>
    /// <param name="nfiq1">NFIQ 1 classification, from 1 (best) through 5 (poorest).</param>
    /// <param name="algorithmVersion">The quality provider's version or pinned model identity.</param>
    /// <param name="confidence">Optional classifier confidence, from zero through one.</param>
    /// <param name="status">The image condition returned by the quality engine.</param>
    /// <returns>The assessment, or an error when the classification, confidence, status, or algorithm identity is invalid.</returns>
    public static Result<FingerprintQualityAssessment, FingerprintError> Create(
        int nfiq1,
        string algorithmVersion,
        Maybe<double> confidence,
        FingerprintQualityStatus status
    ) =>
        FingerprintChecks
            .Require(
                nfiq1 is >= 1 and <= 5
                    && !string.IsNullOrWhiteSpace(algorithmVersion)
                    && Enum.IsDefined(status)
                    && confidence.Match(
                        value => double.IsFinite(value) && value is >= 0 and <= 1,
                        () => true
                    ),
                FingerprintErrorCodes.InvalidQuality,
                "The classification, confidence, status, or algorithm identity is invalid."
            )
            .Map(() =>
                new FingerprintQualityAssessment(nfiq1, algorithmVersion, confidence, status)
            );

    /// <summary>Creates a normally assessed classification without classifier confidence.</summary>
    /// <param name="nfiq1">NFIQ 1 classification, from 1 (best) through 5 (poorest).</param>
    /// <param name="algorithmVersion">The quality provider's version or pinned model identity.</param>
    /// <returns>The assessment, or an error when the classification or algorithm identity is invalid.</returns>
    public static Result<FingerprintQualityAssessment, FingerprintError> Create(
        int nfiq1,
        string algorithmVersion
    ) => Create(nfiq1, algorithmVersion, Maybe<double>.None, FingerprintQualityStatus.Assessed);
}
