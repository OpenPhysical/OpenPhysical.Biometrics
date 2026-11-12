// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal static class CaptureDetailsDescription
{
    internal static string For(CaptureProcessingDetailsSource source, string stage) =>
        source.Evidence.Match(_ => "Selected capture", () => "Sensor image")
        + $" · {source.Original.Width} × {source.Original.Height} · exposure {source.Original.Provenance.Settings.Exposure}\n"
        + source.Processing.Match(
            artifact => $"{artifact.Version} · revision {artifact.ProcessingRevision}\n",
            () => string.Empty
        )
        + "Top-left origin."
        + (
            stage is "Original" or "Processing image"
                ? string.Empty
                : " Mask foreground is black, excluded pixels are white."
        )
        + source.Evidence.Match(
            evidence => $"\nSelection: {evidence.SelectionReason}",
            () => string.Empty
        )
        + Diagnostics(source)
        + source.Evidence.Match(
            evidence =>
                $"\nFeature area: {evidence.Contour.Statistics.FeatureArea} px · inferred holes: {evidence.Contour.Statistics.FilledHoleArea} px"
                + $"\nUncertain area: {evidence.Contour.Statistics.UncertainArea} px · image context: {evidence.Contour.Statistics.ContextArea} px"
                + $"\n{evidence.Contour.Version} · policy {evidence.Contour.PolicyDigest}",
            () => string.Empty
        )
        + (
            stage == "Processing image" && source.Processing.HasNoValue
                ? "\nNo processing image was prepared for this observation."
                : string.Empty
        );

    private static string Diagnostics(CaptureProcessingDetailsSource source) =>
        source.Diagnostics.Match(
            diagnostics =>
                $"\nContact issue: {diagnostics.Issue} · detected regions: {diagnostics.SpatialComponentCount} · accepted regions: {diagnostics.ComponentCount}"
                + $"\nCandidate: {diagnostics.CandidateArea} px · ridge support: {diagnostics.RidgeSupportArea} px"
                + $"\nAccepted contact evidence: {diagnostics.AcceptedArea} px · rejected: {diagnostics.RejectedArea} px"
                + $"\nOriginal signal: {diagnostics.Signal:0.#} · contrast: {diagnostics.Contrast:0.#}"
                + $"\n{diagnostics.Version} · policy {diagnostics.PolicyDigest}",
            () => string.Empty
        );
}
