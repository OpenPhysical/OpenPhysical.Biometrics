// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;

internal static class PreferencesJson
{
    internal static Result<DesktopPreferences, DesktopError> Read(byte[] bytes) =>
        DesktopEdge
            .Try(() => Parse(bytes), "Settings.Format", "The saved settings are not valid JSON.")
            .Bind(result => result);

    internal static byte[] Write(DesktopPreferences preferences) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                schemaVersion = 1,
                comparisonThreshold = preferences.ComparisonThreshold,
                autoCapture = preferences.AutoCapture,
                faceAutoCapture = preferences.FaceAutoCapture,
                faceBackgroundReplacement = preferences.FaceBackgroundReplacement,
                invertImage = preferences.InvertImage,
                autoCaptureDelaySeconds = preferences.AutoCaptureDelaySeconds,
                mouthMaximumOpeningRatio = preferences.MouthPolicy.MaximumOpeningRatio,
                imageAdjustments = new
                {
                    automaticEnhancement = preferences.ImageAdjustments.AutomaticEnhancement,
                    brightness = preferences.ImageAdjustments.Brightness,
                    contrast = preferences.ImageAdjustments.Contrast,
                },
                capturePolicy = CapturePolicyJson.Write(preferences.CapturePolicy),
            }
        );

    private static Result<DesktopPreferences, DesktopError> Parse(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (
            root.ValueKind != JsonValueKind.Object
            || root.EnumerateObject().Count() != 10
            || !root.TryGetProperty("schemaVersion", out var schema)
            || schema.ValueKind != JsonValueKind.Number
            || !schema.TryGetInt32(out var version)
            || version != 1
            || !root.TryGetProperty("autoCaptureDelaySeconds", out var delay)
            || delay.ValueKind != JsonValueKind.Number
            || !delay.TryGetDouble(out var seconds)
        )
            return Invalid();
        return ReadCommon(root)
            .Bind(values =>
                DesktopPreferences
                    .Create(values.Threshold, values.Automatic, values.Invert, seconds)
                    .Map(value =>
                        value with
                        {
                            FaceAutoCapture = values.AutomaticPhoto,
                            FaceBackgroundReplacement = values.ReplaceBackground,
                        }
                    )
            )
            .Bind(preferences => ReadPolicies(root, preferences));
    }

    private static Result<DesktopPreferences, DesktopError> ReadPolicies(
        JsonElement root,
        DesktopPreferences preferences
    ) =>
        ImageAdjustmentJson
            .Read(root)
            .Map(settings => preferences with { ImageAdjustments = settings })
            .Bind(value =>
                CapturePolicyJson.Read(root).Map(policy => value with { CapturePolicy = policy })
            )
            .Bind(value =>
                MouthPolicyInput.Read(root).Map(policy => value with { MouthPolicy = policy })
            );

    private static Result<
        (int Threshold, bool Automatic, bool AutomaticPhoto, bool ReplaceBackground, bool Invert),
        DesktopError
    > ReadCommon(JsonElement root)
    {
        if (
            !root.TryGetProperty("comparisonThreshold", out var threshold)
            || threshold.ValueKind != JsonValueKind.Number
            || !threshold.TryGetInt32(out var value)
            || !root.TryGetProperty("autoCapture", out var auto)
            || auto.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !root.TryGetProperty("faceAutoCapture", out var photo)
            || photo.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !root.TryGetProperty("faceBackgroundReplacement", out var background)
            || background.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !root.TryGetProperty("invertImage", out var invert)
            || invert.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
        )
            return Invalid();
        return (
            value,
            auto.GetBoolean(),
            photo.GetBoolean(),
            background.GetBoolean(),
            invert.GetBoolean()
        );
    }

    private static DesktopError Invalid() =>
        new("Settings.Format", "The saved settings do not match a current settings schema.");
}
