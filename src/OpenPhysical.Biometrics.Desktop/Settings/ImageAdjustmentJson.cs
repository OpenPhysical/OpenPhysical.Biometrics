// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;

internal static class ImageAdjustmentJson
{
    internal static Result<ImageAdjustmentSettings, DesktopError> Read(JsonElement root)
    {
        if (
            !root.TryGetProperty("imageAdjustments", out var settings)
            || settings.ValueKind != JsonValueKind.Object
            || settings.EnumerateObject().Count() != 3
            || !settings.TryGetProperty("automaticEnhancement", out var automatic)
            || automatic.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !settings.TryGetProperty("brightness", out var brightness)
            || brightness.ValueKind != JsonValueKind.Number
            || !brightness.TryGetInt32(out var offset)
            || !settings.TryGetProperty("contrast", out var contrast)
            || contrast.ValueKind != JsonValueKind.Number
            || !contrast.TryGetInt32(out var gain)
        )
            return new DesktopError(
                "Settings.ImageAdjustment",
                "The saved image adjustments are invalid."
            );
        return ImageAdjustmentSettings.Create(automatic.GetBoolean(), offset, gain);
    }
}
