// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;

internal sealed class FaceDraftOverride
{
    private Maybe<FacialCaptureOverride> original;
    internal Maybe<FacialCaptureOverride> Current { get; private set; }

    internal bool CanCreate(Maybe<PortraitPreparationResult> portrait) =>
        Current.HasNoValue
        && portrait.Match(
            value => FacialCaptureOverride.Create(value, DateTimeOffset.UtcNow).IsSuccess,
            () => false
        );

    internal bool Ready(Maybe<PortraitPreparationResult> portrait) =>
        portrait.Match(
            value =>
                FacialPortraitReadiness
                    .Check(
                        value,
                        FacialPreparationPolicy.UnsignedStaging with
                        {
                            CaptureOverride = Current,
                        }
                    )
                    .IsSuccess,
            () => false
        );

    internal UnitResult<DesktopError> Apply(Maybe<PortraitPreparationResult> portrait)
    {
        var created = portrait
            .ToResult(
                new DesktopError(
                    "Face.NoPortrait",
                    "Capture a portrait before overriding a failed test."
                )
            )
            .Bind(value =>
                FacialCaptureOverride
                    .Create(value, DateTimeOffset.UtcNow)
                    .MapError(error => new DesktopError(error.Code, error.Message))
            );
        if (created.IsFailure)
            return created.Error;
        Current = created.Value;
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Retain(Maybe<PortraitPreparationResult> portrait)
    {
        Current = Current.Where(evidence =>
            portrait.Match(value => evidence.Validate(value).IsSuccess, () => false)
        );
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> BeginEdit()
    {
        original = Current;
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> FinishEdit(
        bool apply,
        Maybe<PortraitPreparationResult> portrait
    )
    {
        if (!apply)
            Current = original;
        original = default;
        return Retain(portrait);
    }

    internal UnitResult<DesktopError> Clear()
    {
        Current = original = default;
        return UnitResult.Success<DesktopError>();
    }
}
