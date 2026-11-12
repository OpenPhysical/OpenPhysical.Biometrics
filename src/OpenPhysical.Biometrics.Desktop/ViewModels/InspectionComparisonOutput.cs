// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed record InspectionComparisonOutput(
    string Summary,
    string Report,
    string AlignmentStatus,
    Maybe<FingerprintAlignmentResult> Alignment
)
{
    internal Maybe<bool> Accepted { get; init; }

    internal static InspectionComparisonOutput Evaluate(
        TemplateComparison comparison,
        InspectionComparisonRequest request
    )
    {
        var result = comparison.Compare(
            request.Probe,
            request.AssignedFinger,
            request.Reference,
            request.View,
            request.Threshold
        );
        if (result.IsFailure)
            return new(
                result.Error.Message,
                $"{result.Error.Message} ({result.Error.Code})",
                "Reference alignment unavailable.",
                Maybe<FingerprintAlignmentResult>.None
            );
        var summary = result.Value.Accepted ? "Match" : "No match";
        var report =
            $"{TemplateInspection.DescribeReference(request.Reference)}\n\nBozorth3 score: {result.Value.RawScore} · threshold: {request.Threshold}\n{summary} · desktop-bozorth3-v1";
        return Register(request, summary, report) with
        {
            Accepted = Maybe.From(result.Value.Accepted),
        };
    }

    private static InspectionComparisonOutput Register(
        InspectionComparisonRequest request,
        string summary,
        string report
    )
    {
        var alignment = FingerprintAlignment.Align(
            request.Reference,
            request.View,
            request.Probe.CanonicalRecord(request.AssignedFinger),
            0
        );
        if (alignment.IsFailure)
            return new(
                summary,
                $"{report}\n\n{alignment.Error.Message} ({alignment.Error.Code})",
                "Reference alignment unavailable. The captured image has no point overlay.",
                Maybe<FingerprintAlignmentResult>.None
            );
        var details = FormattableString.Invariant(
            $"{alignment.Value.Pairs.Length} reference correspondences · RMS {alignment.Value.RootMeanSquareError:F2} px at 197 px/cm"
        );
        return new(
            summary,
            $"{report}\n\n{details}",
            "Reference points aligned to this capture.",
            alignment.Value
        );
    }
}
