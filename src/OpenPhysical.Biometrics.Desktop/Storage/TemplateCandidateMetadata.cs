// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal sealed record TemplateCandidateMetadata(
    string OriginalDigest,
    string ProcessedDigest,
    long Sequence,
    DateTimeOffset CapturedAt,
    TemplateQualityEvidenceMetadata Quality,
    double[] Motion,
    double[] ClippedFraction,
    int ReliableCoverage,
    long[] ObservedAt
)
{
    internal static TemplateCandidateMetadata From(CaptureCandidateSummary value) =>
        new(
            value.OriginalDigest,
            value.ProcessedDigest,
            value.Sequence,
            value.CapturedAt,
            TemplateQualityEvidenceMetadata.From(value.Quality),
            OptionalFinite(value.Motion),
            OptionalFinite(value.ClippedFraction),
            value.ReliableCoverage,
            value.ObservedAt.Match(number => new[] { number }, () => Array.Empty<long>())
        );

    internal Result<CaptureCandidateSummary, DesktopError> Read(int pixelCount)
    {
        if (
            !TemplatePngFiles.Hash(OriginalDigest)
            || !TemplatePngFiles.Hash(ProcessedDigest)
            || Sequence < 0
            || CapturedAt == default
            || CapturedAt.Offset != TimeSpan.Zero
            || Quality is null
            || !Valid(Motion, double.MaxValue)
            || !Valid(ClippedFraction, 1)
            || ReliableCoverage < 0
            || ReliableCoverage > pixelCount
            || ObservedAt is null
            || ObservedAt.Length > 1
            || ObservedAt.Any(number => number < 0)
        )
            return TemplatePngFiles.Invalid(
                "A saved burst candidate has invalid identity, quality, or ranking evidence."
            );
        return Quality
            .Read(CaptureImageRole.Processed, ProcessedDigest)
            .Map(quality => new CaptureCandidateSummary(
                OriginalDigest,
                ProcessedDigest,
                Sequence,
                CapturedAt,
                quality,
                Optional(Motion),
                Optional(ClippedFraction),
                ReliableCoverage,
                ObservedAt.Length == 1 ? Maybe.From(ObservedAt[0]) : Maybe<long>.None
            ));
    }

    private static double[] OptionalFinite(Maybe<double> value) =>
        value.Match(
            number => double.IsFinite(number) ? new[] { number } : Array.Empty<double>(),
            () => Array.Empty<double>()
        );

    private static Maybe<double> Optional(double[] value) =>
        value.Length == 1 ? Maybe.From(value[0]) : Maybe<double>.None;

    private static bool Valid(double[] value, double maximum) =>
        value is not null
        && value.Length <= 1
        && value.All(number => double.IsFinite(number) && number >= 0 && number <= maximum);
}
