// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop;

internal static class TemplateUnifiedQualityMetadata
{
    internal static JsonElement Write(Maybe<FingerprintUnifiedQualityAssessment> assessment) =>
        assessment.Match(
            value => JsonSerializer.SerializeToElement(new { value.Score, value.AlgorithmVersion }),
            () => default
        );

    internal static Result<Maybe<FingerprintUnifiedQualityAssessment>, DesktopError> Read(
        JsonElement value
    )
    {
        if (value.ValueKind == JsonValueKind.Undefined)
            return Maybe<FingerprintUnifiedQualityAssessment>.None;
        if (
            value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("Score", out var score)
            || score.ValueKind != JsonValueKind.Number
            || !score.TryGetInt32(out var number)
            || !value.TryGetProperty("AlgorithmVersion", out var version)
            || version.ValueKind != JsonValueKind.String
            || version.GetString() is not { } identity
        )
            return Invalid();
        return FingerprintUnifiedQualityAssessment
            .Create(number, identity)
            .Map(Maybe.From)
            .MapError(_ => Invalid());
    }

    private static DesktopError Invalid() =>
        new(
            "Storage.Manifest",
            "The saved NFIQ 2 assessment must contain a score from 0 to 100 and an algorithm version."
        );
}
