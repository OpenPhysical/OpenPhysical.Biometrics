// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed record EvaluationNfiq1Metadata(
    int Nfiq1,
    byte PivQuality,
    string AlgorithmVersion,
    string Status,
    double[] Confidence
)
{
    internal static EvaluationNfiq1Metadata From(FingerprintQualityAssessment value) =>
        new(
            value.Nfiq1,
            value.PivQuality,
            value.AlgorithmVersion,
            value.Status.ToString(),
            value.Confidence.Match(number => new[] { number }, () => [])
        );
}
