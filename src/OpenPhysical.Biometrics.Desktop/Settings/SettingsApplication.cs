// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal static class SettingsApplication
{
    internal static UnitResult<DesktopError> Apply(
        DesktopPreferences preferences,
        CaptureViewModel capture,
        InspectionViewModel inspection
    )
    {
        capture.AutoCapture = preferences.AutoCapture;
        inspection.Threshold = preferences.ComparisonThreshold;
        return capture
            .ConfigureImageAdjustments(preferences.ImageAdjustments, preferences.CapturePolicy)
            .Bind(() =>
                capture.ConfigureAutoCaptureDelay(
                    TimeSpan.FromSeconds(preferences.AutoCaptureDelaySeconds)
                )
            );
    }
}
