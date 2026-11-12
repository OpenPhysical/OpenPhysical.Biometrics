// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal static class InspectionProbeDescription
{
    internal static string Describe(FingerChoice assigned, Maybe<CapturedFingerprint> capture)
    {
        var assignment = $"Comparison finger: {assigned.Label}";
        if (capture.HasNoValue)
            return $"{assignment}\nNo frozen capture.";
        var diagnostics = CaptureDiagnostics.For(capture);
        return $"{assignment}\nOriginal capture label: {FingerChoice.LabelFor(capture.Value.Finger)}\n"
            + $"{diagnostics.FeaturesFoundText}\n{diagnostics.FeaturesInTemplateText}\n{diagnostics.QualityText}"
            + (diagnostics.HasNfiq2 ? $"\n{diagnostics.Nfiq2Text}" : string.Empty);
    }
}
