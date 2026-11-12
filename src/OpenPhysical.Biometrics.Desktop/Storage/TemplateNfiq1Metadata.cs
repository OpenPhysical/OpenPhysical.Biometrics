// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed record TemplateNfiq1Metadata(
    int Nfiq1,
    byte PivQuality,
    string AlgorithmVersion,
    string Status,
    double[] Confidence
)
{
    internal static JsonElement Write(FingerprintQualityAssessment assessment) =>
        JsonSerializer.SerializeToElement(From(assessment));

    internal static TemplateNfiq1Metadata From(FingerprintQualityAssessment assessment) =>
        new(
            assessment.Nfiq1,
            assessment.PivQuality,
            assessment.AlgorithmVersion,
            assessment.Status.ToString(),
            assessment.Confidence.Match(value => new[] { value }, () => Array.Empty<double>())
        );

    internal static Result<FingerprintQualityAssessment, DesktopError> Read(JsonElement element) =>
        DesktopEdge
            .Try(
                () => element.Deserialize<TemplateNfiq1Metadata>(),
                "Storage.Manifest",
                "The saved NFIQ 1 assessment is invalid."
            )
            .Bind(value =>
                value is not null
                    ? value.ToAssessment()
                    : TemplatePngFiles.Invalid("The NFIQ 1 assessment is missing.")
            );

    internal Result<FingerprintQualityAssessment, DesktopError> ToAssessment()
    {
        if (
            Confidence is null
            || Confidence.Length > 1
            || !Enum.TryParse<FingerprintQualityStatus>(Status, out var status)
            || !Enum.IsDefined(status)
        )
            return TemplatePngFiles.Invalid("The saved NFIQ 1 status or confidence is invalid.");
        return FingerprintQualityAssessment
            .Create(
                Nfiq1,
                AlgorithmVersion,
                Confidence.Length == 1 ? Maybe.From(Confidence[0]) : Maybe<double>.None,
                status
            )
            .MapError(error => TemplatePngFiles.Invalid(error.Message))
            .Ensure(
                value => value.PivQuality == PivQuality,
                TemplatePngFiles.Invalid("The saved NFIQ 1 PIV encoding is inconsistent.")
            );
    }
}
