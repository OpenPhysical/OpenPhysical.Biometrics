// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;

/// <summary>An application-selected, versioned acceptance policy for raw Bozorth3 scores.</summary>
public sealed class FingerprintMatchingPolicy
{
    private FingerprintMatchingPolicy(string identifier, int minimumScore)
    {
        Identifier = identifier;
        MinimumScore = minimumScore;
    }

    /// <summary>Gets the application policy name and revision.</summary>
    public string Identifier { get; }

    /// <summary>Gets the minimum accepted raw Bozorth3 score.</summary>
    public int MinimumScore { get; }

    /// <summary>Creates an explicit comparison policy.</summary>
    /// <param name="identifier">The application's policy name and revision.</param>
    /// <param name="minimumScore">The minimum accepted Bozorth3 score.</param>
    /// <returns>The policy, or an error when the identifier is blank or the score is negative.</returns>
    public static Result<FingerprintMatchingPolicy, FingerprintError> Create(
        string identifier,
        int minimumScore
    ) =>
        FingerprintChecks
            .Require(
                !string.IsNullOrWhiteSpace(identifier),
                FingerprintErrorCodes.InvalidPolicy,
                "A policy identifier is required."
            )
            .Bind(() =>
                FingerprintChecks.Require(
                    minimumScore >= 0,
                    FingerprintErrorCodes.InvalidScore,
                    "The minimum score cannot be negative."
                )
            )
            .Map(() => new FingerprintMatchingPolicy(identifier, minimumScore));

    /// <summary>Applies this policy to a raw score from any supported capture or template workflow.</summary>
    /// <param name="rawScore">The nonnegative raw Bozorth3 score.</param>
    /// <returns>The score, policy decision, and exact policy identifier, or an error when the score is negative.</returns>
    public Result<FingerprintComparisonResult, FingerprintError> Evaluate(int rawScore) =>
        FingerprintChecks
            .Require(
                rawScore >= 0,
                FingerprintErrorCodes.InvalidScore,
                "A raw score cannot be negative."
            )
            .Map(() =>
                new FingerprintComparisonResult(rawScore, rawScore >= MinimumScore, Identifier)
            );
}
