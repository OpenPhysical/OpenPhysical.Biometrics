// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal sealed record CaptureAdjustmentDraft(
    CapturedFingerprint Original,
    CapturedFingerprint CompletedSource,
    Maybe<CapturedFingerprint> Ready,
    ImageAdjustmentSettings Settings,
    string Error
)
{
    internal ImageAdjustmentSettings Initial =>
        Original
            .Processing.Map(value => CaptureEditConfiguration.Image(value.Artifact.Policy))
            .GetValueOrDefault(ImageAdjustmentSettings.Original);
    internal bool HasChanges => Settings != Initial;
    internal bool CanApply =>
        !HasChanges
        || Error.Length == 0
            && Ready.HasValue
            && Ready.Value.Finger == Original.Finger
            && ReferenceEquals(Ready.Value.Frame, Original.Frame);

    internal static CaptureAdjustmentDraft Create(CapturedFingerprint original)
    {
        var initial = original
            .Processing.Map(value => CaptureEditConfiguration.Image(value.Artifact.Policy))
            .GetValueOrDefault(ImageAdjustmentSettings.Original);
        return new(
            original,
            original,
            default,
            initial,
            original.Processing.HasValue
                ? string.Empty
                : "This capture has no retained original for image adjustments."
        );
    }

    internal CaptureAdjustmentDraft Begin(ImageAdjustmentSettings settings) =>
        this with
        {
            Settings = settings,
            Ready = default,
            Error = string.Empty,
        };

    internal CaptureAdjustmentDraft Complete(Result<CapturedFingerprint, DesktopError> result) =>
        result.IsSuccess
            ? this with
            {
                CompletedSource = Settings == Initial ? CompletedSource : result.Value,
                Ready = result.Value,
                Error = string.Empty,
            }
            : this with
            {
                Ready = default,
                Error = result.Error.Message,
            };
}
