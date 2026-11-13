// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal static class SettingsApplication
{
    internal static async Task<UnitResult<DesktopError>> ApplyAsync(
        DesktopPreferences preferences,
        CaptureViewModel capture,
        InspectionViewModel inspection,
        FaceCaptureViewModel face
    )
    {
        capture.AutoCapture = preferences.AutoCapture;
        inspection.Threshold = preferences.ComparisonThreshold;
        var fingerprint = capture
            .ConfigureImageAdjustments(preferences.ImageAdjustments, preferences.CapturePolicy)
            .Bind(() =>
                capture.ConfigureAutoCaptureDelay(
                    TimeSpan.FromSeconds(preferences.AutoCaptureDelaySeconds)
                )
            );
        if (fingerprint.IsFailure)
            return fingerprint;
        var mouth = await face.ConfigureMouthPolicyAsync(preferences.MouthPolicy);
        if (mouth.IsFailure)
            return mouth;
        var background = await face.ConfigureBackgroundAsync(preferences.FaceBackgroundReplacement);
        return background.IsFailure
            ? background
            : await face.ConfigureAutoCaptureAsync(
                preferences.FaceAutoCapture,
                TimeSpan.FromSeconds(preferences.AutoCaptureDelaySeconds)
            );
    }
}
