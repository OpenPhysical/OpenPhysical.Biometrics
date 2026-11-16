// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;

/// <summary>Application hysteresis for a conservative two-score comparison; these are not NIST thresholds.</summary>
public sealed class FingerprintQualitySelectionPolicy
{
    private FingerprintQualitySelectionPolicy(int minimumNfiq1Gain, int minimumNfiq2Gain)
    {
        MinimumNfiq1Gain = minimumNfiq1Gain;
        MinimumNfiq2Gain = minimumNfiq2Gain;
    }

    /// <summary>Gets application defaults of one NFIQ 1 class or five NFIQ 2 points.</summary>
    public static FingerprintQualitySelectionPolicy Default { get; } = new(1, 5);

    /// <summary>Gets the required decrease in NFIQ 1 class, from one through four.</summary>
    public int MinimumNfiq1Gain { get; }

    /// <summary>Gets the required increase in NFIQ 2 score, from one through 100.</summary>
    public int MinimumNfiq2Gain { get; }

    /// <summary>Validates meaningful improvements on the independent score scales.</summary>
    /// <param name="minimumNfiq1Gain">Required class decrease, one through four.</param>
    /// <param name="minimumNfiq2Gain">Required score increase, one through 100.</param>
    /// <returns>A policy or a precise invalid-selection-policy failure.</returns>
    public static Result<FingerprintQualitySelectionPolicy, FingerprintError> Create(
        int minimumNfiq1Gain,
        int minimumNfiq2Gain
    ) =>
        FingerprintChecks
            .Require(
                minimumNfiq1Gain is >= 1 and <= 4 && minimumNfiq2Gain is >= 1 and <= 100,
                FingerprintErrorCodes.InvalidSelectionPolicy,
                "Quality selection requires a positive gain within each score scale."
            )
            .Map(() => new FingerprintQualitySelectionPolicy(minimumNfiq1Gain, minimumNfiq2Gain));
}
