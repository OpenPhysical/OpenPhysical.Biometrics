// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

internal static class CaptureSelectionFactory
{
    internal static Result<CaptureBurstSelection, DesktopError> Choose(
        ImmutableArray<CaptureCandidateEvaluation> evaluated,
        FingerprintQualitySelectionPolicy policy
    ) =>
        evaluated.Length == 1 && !evaluated[0].Processed.Evidence.IsAssessed
            ? Result.Failure<CaptureBurstSelection, DesktopError>(
                CaptureQualityFailure.From(evaluated[0].Processed.Evidence)
            )
            : CaptureQualitySelection
                .Select(evaluated.Select(value => value.Summary).ToImmutableArray(), policy)
                .Map(choice => new CaptureBurstSelection(
                    evaluated[choice.SelectedIndex],
                    evaluated[choice.BaselineIndex],
                    evaluated,
                    policy,
                    choice.Reason
                ));
}
