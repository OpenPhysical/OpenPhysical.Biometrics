// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

/// <summary>An application-selected, versioned acceptance policy for raw Bozorth3 scores.</summary>
public sealed class FingerprintMatchingPolicy
{
    /// <summary>Creates an explicit comparison policy.</summary>
    /// <param name="identifier">The application's policy name and revision.</param>
    /// <param name="minimumScore">The minimum accepted Bozorth3 score.</param>
    /// <exception cref="ArgumentException">The policy identifier is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The score is negative.</exception>
    public FingerprintMatchingPolicy(string identifier, int minimumScore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumScore);
        Identifier = identifier;
        MinimumScore = minimumScore;
    }

    /// <summary>Applies this policy to a raw score from any supported capture or template workflow.</summary>
    /// <param name="rawScore">The nonnegative raw Bozorth3 score.</param>
    /// <returns>The score, policy decision, and exact policy identifier.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The score is negative.</exception>
    public FingerprintComparisonResult Evaluate(int rawScore)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rawScore);
        return new(rawScore, rawScore >= MinimumScore, Identifier);
    }

    /// <summary>Gets the application policy name and revision.</summary>
    public string Identifier { get; }

    /// <summary>Gets the minimum accepted raw Bozorth3 score.</summary>
    public int MinimumScore { get; }
}

/// <summary>A comparison result bound to the exact application policy used.</summary>
/// <param name="RawScore">The unmodified Bozorth3 score.</param>
/// <param name="Accepted">Whether the score meets the supplied policy.</param>
/// <param name="PolicyIdentifier">The applied policy name and revision.</param>
public sealed record FingerprintComparisonResult(
    int RawScore,
    bool Accepted,
    string PolicyIdentifier
);

/// <summary>Compares a fresh capture with an explicitly selected fingerprint reference.</summary>
public sealed class FingerprintComparisonService
{
    private readonly IFingerprintEngine engine;

    /// <summary>Creates a service using the selected fingerprint engine.</summary>
    /// <param name="engine">The extraction and matching implementation.</param>
    /// <exception cref="ArgumentNullException">The engine is null.</exception>
    public FingerprintComparisonService(IFingerprintEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        this.engine = engine;
    }

    /// <summary>Compares a capture with a reference authenticated by the caller.</summary>
    /// <param name="probe">The fresh capture; its finger must match the selected reference.</param>
    /// <param name="reference">The caller-authenticated canonical reference record.</param>
    /// <param name="finger">The reference finger to select.</param>
    /// <param name="viewNumber">The explicit reference view number.</param>
    /// <param name="policy">The application's versioned matching policy.</param>
    /// <returns>The raw score and policy decision.</returns>
    /// <exception cref="ArgumentNullException">An input is null.</exception>
    /// <exception cref="ArgumentException">The probe identifies a different finger.</exception>
    public FingerprintComparisonResult Compare(
        FingerprintCapture probe,
        FingerprintRecord reference,
        FingerPosition finger,
        byte viewNumber,
        FingerprintMatchingPolicy policy
    )
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(policy);
        if (probe.Finger != finger)
            throw new ArgumentException(
                "The probe and selected reference fingers must agree.",
                nameof(finger)
            );
        var template = FingerprintRecordAdapter.ToMatchingTemplate(reference, finger, viewNumber);
        var score = engine.Match(engine.Extract(probe.Image), template);
        return policy.Evaluate(score);
    }
}
