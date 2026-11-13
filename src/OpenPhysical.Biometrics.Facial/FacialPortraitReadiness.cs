// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;

/// <summary>Shares source-supported portrait and override readiness between encoding and operator workflows.</summary>
public static class FacialPortraitReadiness
{
    /// <summary>Checks the actual portrait without encoding it or changing its findings.</summary>
    /// <param name="portrait">Source-bound prepared or rejected inspection image.</param>
    /// <param name="policy">Required acquisition evidence and explicit unsigned-staging override.</param>
    /// <returns>Readiness or the specific review/override failure that blocks encoding.</returns>
    public static UnitResult<FacialError> Check(
        PortraitPreparationResult portrait,
        FacialPreparationPolicy policy
    )
    {
        if (!PortraitInspectionData.IsUsable(portrait))
            return new FacialError(
                FacialErrorCodes.CaptureReviewRequired,
                "This capture has no usable source-supported portrait."
            );
        if (
            portrait.Status == PortraitStatus.Prepared
            && !portrait.Findings.Any(f => f.Action == PortraitFindingAction.Retake)
        )
            return UnitResult.Success<FacialError>();
        return policy.AllowCaptureOverrides && policy.CaptureOverride.HasValue
            ? policy.CaptureOverride.Value.Validate(portrait)
            : UnitResult.Failure(
                new FacialError(
                    FacialErrorCodes.CaptureReviewRequired,
                    "Retake this capture or explicitly override its eligible automatic failures."
                )
            );
    }
}
