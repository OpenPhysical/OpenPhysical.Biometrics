// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;

/// <summary>A continuous fingerprint utility score, independent of PIV's NFIQ 1 encoding.</summary>
public sealed class FingerprintUnifiedQualityAssessment
{
    private FingerprintUnifiedQualityAssessment(int score, string algorithmVersion)
    {
        Score = score;
        AlgorithmVersion = algorithmVersion;
    }

    /// <summary>Gets the score from zero through 100; higher scores indicate greater expected recognition utility.</summary>
    public int Score { get; }

    /// <summary>Gets the algorithm version and model identity used for this assessment.</summary>
    public string AlgorithmVersion { get; }

    /// <summary>Validates a continuous assessment without assigning quality classes or PIV values.</summary>
    /// <param name="score">A continuous score from zero through 100, inclusive.</param>
    /// <param name="algorithmVersion">The algorithm version and model identity.</param>
    /// <returns>The assessment, or an error for an invalid score or missing algorithm identity.</returns>
    public static Result<FingerprintUnifiedQualityAssessment, FingerprintError> Create(
        int score,
        string algorithmVersion
    ) =>
        FingerprintChecks
            .Require(
                score is >= 0 and <= 100 && !string.IsNullOrWhiteSpace(algorithmVersion),
                FingerprintErrorCodes.InvalidQuality,
                "The unified quality score or algorithm identity is invalid."
            )
            .Map(() => new FingerprintUnifiedQualityAssessment(score, algorithmVersion));
}
