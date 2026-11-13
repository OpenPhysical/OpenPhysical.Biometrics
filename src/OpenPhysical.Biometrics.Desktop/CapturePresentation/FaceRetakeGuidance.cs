// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;

internal static class FaceRetakeGuidance
{
    private static ImmutableArray<PortraitFinding> Failures(
        Maybe<PortraitPreparationResult> portrait
    ) =>
        portrait.Match(
            value =>
                value
                    .Findings.Where(finding => finding.Code != "EstimatedHeadBoundary")
                    .ToImmutableArray(),
            () => ImmutableArray<PortraitFinding>.Empty
        );

    internal static Maybe<string> Retake(Maybe<PortraitPreparationResult> portrait) =>
        Failures(portrait)
            .Where(value => value.Action == PortraitFindingAction.Retake)
            .Select(value => Maybe.From(value.Message))
            .FirstOrDefault();

    internal static string Message(Maybe<PortraitPreparationResult> portrait)
    {
        var names = Failures(portrait)
            .Select(value =>
                string.IsNullOrWhiteSpace(value.TestName)
                    ? value.Message.TrimEnd('.')
                    : value.TestName
            )
            .Distinct()
            .ToArray();
        return names.Length == 0
            ? string.Empty
            : "Photo failed the following tests: " + string.Join(", ", names) + ".";
    }
}
