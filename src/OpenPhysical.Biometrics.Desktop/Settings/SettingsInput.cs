// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Globalization;
using CSharpFunctionalExtensions;

internal static class SettingsInput
{
    internal static Result<DesktopPreferences, DesktopError> Parse(
        string threshold,
        string delay,
        string mouthPercent,
        bool automatic,
        bool invert
    ) =>
        ParseThreshold(threshold)
            .Bind(score =>
                ParseDelay(delay)
                    .Bind(seconds => DesktopPreferences.Create(score, automatic, invert, seconds))
                    .Bind(value =>
                        MouthPolicyInput
                            .ParsePercent(mouthPercent)
                            .Map(policy => value with { MouthPolicy = policy })
                    )
            );

    internal static DesktopPreferences Editing(
        DesktopPreferences active,
        string threshold,
        string delay,
        string mouthPercent,
        bool automatic,
        bool invert
    )
    {
        var score = ParseThreshold(threshold);
        var seconds = ParseDelay(delay);
        var mouth = MouthPolicyInput.ParsePercent(mouthPercent);
        return DesktopPreferences
            .Create(
                score.IsSuccess ? score.Value : active.ComparisonThreshold,
                automatic,
                invert,
                seconds.IsSuccess ? seconds.Value : active.AutoCaptureDelaySeconds
            )
            .Value with
        {
            ImageAdjustments = active.ImageAdjustments,
            CapturePolicy = active.CapturePolicy,
            MouthPolicy = mouth.IsSuccess ? mouth.Value : active.MouthPolicy,
            FaceAutoCapture = active.FaceAutoCapture,
            FaceBackgroundReplacement = active.FaceBackgroundReplacement,
        };
    }

    private static Result<int, DesktopError> ParseThreshold(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? DesktopPreferences.CheckThreshold(value)
            : new DesktopError(
                "Settings.Threshold",
                "Comparison threshold must be an integer from 0 to 10000."
            );

    private static Result<double, DesktopError> ParseDelay(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? DesktopPreferences.CheckDelay(value)
            : new DesktopError(
                "Settings.Delay",
                "Automatic capture delay must be a finite number from 0 to 10 seconds."
            );
}
