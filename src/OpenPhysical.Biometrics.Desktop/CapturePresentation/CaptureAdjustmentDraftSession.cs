// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal sealed class CaptureAdjustmentDraftSession
{
    private Maybe<CaptureAdjustmentDraft> draft;
    private Maybe<CaptureEditConfiguration> configuration;
    internal CaptureAdjustmentDraft Current => draft.Value;

    internal UnitResult<DesktopError> Open(CapturedFingerprint source, CaptureViewModel capture)
    {
        draft = CaptureAdjustmentDraft.Create(source);
        configuration = capture.CreateEditConfiguration();
        return UnitResult.Success<DesktopError>();
    }

    internal UnitResult<DesktopError> Begin(ImageAdjustmentSettings settings) =>
        Update(Current.Begin(settings));

    internal UnitResult<DesktopError> Complete(Result<CapturedFingerprint, DesktopError> result) =>
        Update(Current.Complete(result));

    internal UnitResult<DesktopError> Fail(DesktopError error) =>
        Update(Current with { Error = error.Message });

    internal async Task<Result<CapturedFingerprint, DesktopError>> PreviewAsync(
        CaptureViewModel capture,
        ImageAdjustmentSettings settings
    )
    {
        var value = Current;
        if (settings == value.Initial)
        {
            var original = await capture.ReviewAcceptedAsync(value.Original);
            return original.IsSuccess ? value.Original : original.Error;
        }
        var configured = configuration.Value.Configure(value.Original, settings);
        if (configured.IsFailure)
            return configured.Error;
        var adjusted = await capture.AdjustAcceptedAsync(value.CompletedSource);
        return adjusted.IsFailure
            ? adjusted.Error
            : capture.Captured.ToResult(
                new DesktopError("Capture.Adjustment", "The latest capture preview is not ready.")
            );
    }

    internal async Task<UnitResult<DesktopError>> RestoreAsync(
        CaptureViewModel capture,
        CapturedFingerprint selected
    )
    {
        var shown = await capture.ReviewAcceptedAsync(selected);
        var restored = await capture.RestoreEditConfigurationAsync(configuration.Value);
        return shown.IsFailure ? shown : restored;
    }

    internal UnitResult<DesktopError> Clear()
    {
        draft = default;
        configuration = default;
        return UnitResult.Success<DesktopError>();
    }

    private UnitResult<DesktopError> Update(CaptureAdjustmentDraft value)
    {
        draft = value;
        return UnitResult.Success<DesktopError>();
    }
}
