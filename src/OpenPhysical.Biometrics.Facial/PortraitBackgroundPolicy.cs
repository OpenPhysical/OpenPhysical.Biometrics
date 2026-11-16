// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

/// <summary>Application choices for frozen-photo median-background replacement.</summary>
public sealed record PortraitBackgroundPolicy
{
    private PortraitBackgroundPolicy(
        double confidence,
        int guard,
        int feather,
        double defringe,
        double transition
    )
    {
        MaximumBackgroundPersonConfidence = confidence;
        ProtectionRadiusPixels = guard;
        FeatherPixels = feather;
        DefringeRadiusPixels = defringe;
        ConfidenceTransitionWidth = transition;
    }

    /// <summary>Gets median original-background color with tight facial and person-boundary protection.</summary>
    public static PortraitBackgroundPolicy Default { get; } = new(.55, 2, 1, 1, .2);

    /// <summary>Gets whether the saved portrait uses the replacement background.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets the largest person probability eligible for background editing.</summary>
    public double MaximumBackgroundPersonConfidence { get; }

    /// <summary>Gets the unchanged circular buffer around the actual facial hull, in output pixels.</summary>
    public int ProtectionRadiusPixels { get; }

    /// <summary>Gets the isotropic alpha smoothing radius in output pixels.</summary>
    public int FeatherPixels { get; }

    /// <summary>Gets the circular foreground contraction radius, without changing source RGB pixels.</summary>
    public double DefringeRadiusPixels { get; }

    /// <summary>Gets the probability interval over which foreground opacity changes smoothly.</summary>
    public double ConfidenceTransitionWidth { get; }

    /// <summary>Identifies this application policy, independently of PIV image compliance.</summary>
    public string PolicyId => "person-background-soft-median-v1";

    /// <summary>Validates editing parameters without changing source pixels.</summary>
    /// <param name="confidence">Maximum background person probability, greater than zero through .9.</param>
    /// <param name="guard">Protected boundary radius, zero through sixteen output pixels.</param>
    /// <param name="feather">Alpha smoothing radius, zero through sixteen output pixels.</param>
    /// <param name="defringe">Circular contraction radius, zero through four output pixels.</param>
    /// <param name="transition">Confidence transition width, greater than zero through one.</param>
    /// <returns>The immutable policy or a typed policy error.</returns>
    public static Result<PortraitBackgroundPolicy, FacialError> Create(
        double confidence,
        int guard,
        int feather,
        double defringe,
        double transition
    ) =>
        double.IsFinite(confidence)
        && confidence > 0
        && confidence <= .9
        && guard is >= 0 and <= 16
        && feather is >= 0 and <= 16
        && double.IsFinite(defringe)
        && defringe is >= 0 and <= 4
        && double.IsFinite(transition)
        && transition > 0
        && transition <= 1
            ? new PortraitBackgroundPolicy(confidence, guard, feather, defringe, transition)
            : new FacialError(
                FacialErrorCodes.InvalidBackgroundPolicy,
                "Background replacement settings are invalid."
            );
}
