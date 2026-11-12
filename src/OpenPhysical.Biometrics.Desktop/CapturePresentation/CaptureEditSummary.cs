// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

internal static class CaptureEditSummary
{
    internal static string For(ImageAdjustmentSettings original, ImageAdjustmentSettings draft)
    {
        var changes = new List<string>();
        if (original.Brightness != draft.Brightness)
            changes.Add($"Brightness {original.Brightness} → {draft.Brightness}");
        if (original.Contrast != draft.Contrast)
            changes.Add($"Contrast {original.Contrast} → {draft.Contrast}");
        if (original.AutomaticEnhancement != draft.AutomaticEnhancement)
            changes.Add(
                $"Automatic enhancement {State(original.AutomaticEnhancement)} → {State(draft.AutomaticEnhancement)}"
            );
        return changes.Count == 0
            ? "No changes. The original capture is unchanged."
            : string.Join(" · ", changes);
    }

    private static string State(bool enabled) => enabled ? "on" : "off";
}
