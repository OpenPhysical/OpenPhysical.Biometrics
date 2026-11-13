// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Globalization;
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;

internal static class MouthPolicyInput
{
    internal static Result<PortraitMouthPolicy, DesktopError> ParsePercent(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
            ? FromRatio(percent / 100)
            : Invalid();

    internal static Result<PortraitMouthPolicy, DesktopError> Read(JsonElement root) =>
        root.TryGetProperty("mouthMaximumOpeningRatio", out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var ratio)
            ? FromRatio(ratio)
            : new DesktopError("Settings.Format", "The saved mouth policy is missing or invalid.");

    private static Result<PortraitMouthPolicy, DesktopError> FromRatio(double ratio) =>
        PortraitMouthPolicy.Create(ratio).MapError(_ => Invalid());

    private static DesktopError Invalid() =>
        new(
            "Settings.MouthTolerance",
            "Mouth closure tolerance must be a finite percentage greater than 0 and less than 100."
        );
}
