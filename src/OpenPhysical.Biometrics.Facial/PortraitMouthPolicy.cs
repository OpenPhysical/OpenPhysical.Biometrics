// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

/// <summary>An application policy for closed-mouth landmark assessment.</summary>
/// <remarks>The ratio limit is an adjustable application tolerance, not an ISO or PIV formula.</remarks>
public sealed record PortraitMouthPolicy
{
    private PortraitMouthPolicy(double maximumOpeningRatio) =>
        MaximumOpeningRatio = maximumOpeningRatio;

    /// <summary>Gets the largest permitted inner-lip separation divided by inner mouth width.</summary>
    public double MaximumOpeningRatio { get; }

    /// <summary>Gets the versioned measurement policy identifier.</summary>
    public string PolicyId => "inner-lip-opening-v1";

    /// <summary>Gets the conservative default tolerance of four percent of inner mouth width.</summary>
    public static PortraitMouthPolicy Default { get; } = new(.04);

    /// <summary>Creates a finite, positive ratio limit below one mouth width.</summary>
    /// <param name="maximumOpeningRatio">Application tolerance; no normalization is applied.</param>
    /// <returns>An immutable policy or a typed configuration error.</returns>
    public static Result<PortraitMouthPolicy, FacialError> Create(double maximumOpeningRatio) =>
        double.IsFinite(maximumOpeningRatio) && maximumOpeningRatio is > 0 and < 1
            ? new PortraitMouthPolicy(maximumOpeningRatio)
            : new FacialError(
                FacialErrorCodes.InvalidMouthPolicy,
                "The mouth opening tolerance must be a finite ratio greater than zero and less than one."
            );
}
