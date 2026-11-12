// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Biometrics.Formats.Fingerprints;
using OpenPhysical.Biometrics.Piv;

namespace OpenPhysical.Biometrics.Desktop;

internal static class TemplateInspection
{
    internal static string DescribeReference(FingerprintRecord record)
    {
        var findings = PivBiometricProfile.Validate(record);
        var profile =
            findings.Length == 0
                ? "PIV profile: passed"
                : "PIV profile:\n"
                    + string.Join("\n", findings.Select(value => $"{value.Code}: {value.Field}"));
        return $"Reference template\nFile structure: valid ANSI/INCITS 378-2004\n{record.Width} × {record.Height} · {record.XResolution} × {record.YResolution} px/cm\n"
            + $"Product owner: 0x{record.ProductOwner:X4} · type: 0x{record.ProductType:X4}\n"
            + $"Capture equipment: 0x{record.CaptureEquipment:X4} · views: {record.Views.Length}\n"
            + string.Join(
                "\n",
                record.Views.Select(view =>
                    $"{FingerChoice.LabelFor((Fingerprints.FingerPosition)view.FingerPosition)}: {view.Minutiae.Length} minutiae · quality {view.Quality}"
                )
            )
            + $"\n\n{profile}";
    }
}
