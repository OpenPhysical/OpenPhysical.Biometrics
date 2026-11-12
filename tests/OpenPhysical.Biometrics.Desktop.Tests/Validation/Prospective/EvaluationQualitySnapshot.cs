// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed record EvaluationQualitySnapshot(
    EvaluationNfiq1Metadata[] Nfiq1,
    FingerprintUnifiedQualityAssessment[] Nfiq2,
    ImmutableArray<DesktopError> Diagnostics
)
{
    internal static EvaluationQualitySnapshot From(CaptureImageQualityEvidence evidence) =>
        new(
            [EvaluationNfiq1Metadata.From(evidence.Nfiq1)],
            evidence.Nfiq2.Match(value => new[] { value }, () => []),
            evidence.Diagnostics
        );

    internal static EvaluationQualitySnapshot Missing(DesktopError error) => new([], [], [error]);
}
