// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Summarizes measured image quality and the features retained in a template.</summary>
/// <param name="FeaturesFoundText">Native extracted-feature summary.</param>
/// <param name="FeaturesInTemplateText">Encoded-feature summary.</param>
/// <param name="QualityText">Measured selected-image quality.</param>
/// <param name="CaptureAdvice">Capture-specific retry advice.</param>
public sealed record CaptureDiagnostics(
    string FeaturesFoundText,
    string FeaturesInTemplateText,
    string QualityText,
    string CaptureAdvice
)
{
    /// <summary>Gets the optional NFIQ 2 score summary.</summary>
    public string Nfiq2Text { get; init; } = string.Empty;

    /// <summary>Gets whether NFIQ 2 was assessed.</summary>
    public bool HasNfiq2 => Nfiq2Text.Length > 0;

    /// <summary>Gets the original-image control quality summary.</summary>
    public string OriginalQualityText { get; init; } = string.Empty;

    internal static string Describe(CapturedFingerprint capture) =>
        $"{FingerChoice.LabelFor(capture.Finger)} · {capture.View.Minutiae.Length} minutiae · {QualityLabel(capture.Quality)} · template quality: {capture.View.Quality}"
        + capture.Nfiq2.Match(quality => $" · {UnifiedQualityLabel(quality)}", () => string.Empty);

    internal static CaptureDiagnostics For(Maybe<CapturedFingerprint> capture) =>
        capture.Match(
            value => new CaptureDiagnostics(
                value.ExtractedCount.Match(
                    count => $"Found {FeatureCount(count)}",
                    () => "Not assessed"
                ),
                $"Template: {FeatureCount(value.View.Minutiae.Length)}",
                QualityLabel(value.Quality),
                value.View.Minutiae.Length <= 10
                || value.Quality.Nfiq1 > 3
                || value.Quality.Status != FingerprintQualityStatus.Assessed
                    ? "Try another capture for more ridge detail."
                    : string.Empty
            )
            {
                Nfiq2Text = value.Nfiq2.Match(UnifiedQualityLabel, () => string.Empty),
                OriginalQualityText = value.Processing.Match(
                    evidence =>
                        $"Original image: {QualityLabel(evidence.OriginalAssessment.Nfiq1)}"
                        + evidence.OriginalAssessment.Nfiq2.Match(
                            score => $" · {UnifiedQualityLabel(score)}",
                            () => string.Empty
                        ),
                    () => string.Empty
                ),
            },
            () =>
                new CaptureDiagnostics(
                    "Waiting for capture",
                    "No template yet",
                    "Not assessed",
                    "Position the selected finger, then capture."
                )
        );

    private static string FeatureCount(int count) =>
        $"{count} {(count == 1 ? "feature" : "features")}";

    private static string UnifiedQualityLabel(FingerprintUnifiedQualityAssessment assessment) =>
        $"NFIQ 2: {assessment.Score}/100 (higher is better)";

    private static string QualityLabel(FingerprintQualityAssessment assessment)
    {
        if (assessment.Status != FingerprintQualityStatus.Assessed)
            return "NFIQ 1 not assessed";
        var meaning = assessment.Nfiq1 switch
        {
            1 => "excellent",
            2 => "very good",
            3 => "good",
            4 => "fair",
            _ => "poor",
        };
        return $"NFIQ 1 class {assessment.Nfiq1} ({meaning})";
    }
}
