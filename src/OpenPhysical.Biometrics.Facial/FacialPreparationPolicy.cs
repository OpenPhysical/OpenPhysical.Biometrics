// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

/// <summary>Controls acquisition evidence required before encoding; reviewed geometry and frontal review remain mandatory.</summary>
/// <param name="RequireOpticalResolutionConfirmation">Whether unknown optical resolution prevents record preparation.</param>
/// <param name="AllowCaptureOverrides">Whether explicit source-bound automatic-check overrides permit unsigned staging.</param>
public sealed record FacialPreparationPolicy(
    bool RequireOpticalResolutionConfirmation,
    bool AllowCaptureOverrides
)
{
    /// <summary>Gets the attended PIV acquisition policy used by the existing preparation entry points.</summary>
    public static FacialPreparationPolicy StrictPiv { get; } = new(true, false);

    /// <summary>Gets unsigned staging policy, which retains unconfirmed optical evidence without inventing qualification.</summary>
    public static FacialPreparationPolicy UnsignedStaging { get; } = new(false, true);

    /// <summary>Gets the explicit operator acknowledgement; it never changes the original failed findings.</summary>
    public Maybe<FacialCaptureOverride> CaptureOverride { get; init; }
}
