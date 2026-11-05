// SPDX-License-Identifier: GPL-2.0-or-later

using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Capture;

/// <summary>A qualification assertion linked to an external recorded profile.</summary>
public sealed record CaptureQualification
{
    private CaptureQualification(bool qualified, string profileId)
    {
        IsQualified = qualified;
        ProfileId = profileId;
    }

    /// <summary>Gets whether the referenced profile has been qualified.</summary>
    public bool IsQualified { get; }

    /// <summary>Gets the evidence profile identifier, empty for experimental acquisition.</summary>
    public string ProfileId { get; }

    /// <summary>Gets unqualified experimental status.</summary>
    public static CaptureQualification Experimental { get; } = new(false, string.Empty);

    /// <summary>Binds a qualification assertion to its evidence profile.</summary>
    /// <param name="profileId">Identifier of recorded qualification evidence.</param>
    /// <returns>A qualified assertion or invalid-argument failure.</returns>
    public static Result<CaptureQualification, CaptureError> Qualified(string profileId) =>
        !string.IsNullOrWhiteSpace(profileId) && profileId.Length <= 128
            ? new CaptureQualification(true, profileId)
            : new CaptureError(
                CaptureErrorCodes.InvalidArgument,
                "Qualification requires a profile identifier."
            );
}
