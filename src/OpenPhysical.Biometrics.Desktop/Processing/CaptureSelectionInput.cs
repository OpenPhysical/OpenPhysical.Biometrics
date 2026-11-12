// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class CaptureSelectionInput
{
    internal static UnitResult<DesktopError> Check(
        ImmutableArray<CaptureCandidateSummary> candidates
    )
    {
        if (candidates.IsDefaultOrEmpty)
            return UnitResult.Success<DesktopError>();
        foreach (var candidate in candidates)
        {
            if (
                !candidate.Motion.Match(value => double.IsFinite(value) && value >= 0, () => true)
                || !candidate.ClippedFraction.Match(
                    value => double.IsFinite(value) && value is >= 0 and <= 1,
                    () => true
                )
                || candidate.ReliableCoverage < 0
            )
                return UnitResult.Failure(
                    new DesktopError(
                        "Capture.InvalidSelectionInput",
                        "Motion, clipping and supported-area measurements must be finite and within their valid ranges."
                    )
                );
            var operational = candidate
                .Quality.Diagnostics.Where(error =>
                    error.Code
                        is not "QualityImageNotAssessable"
                            and not "Capture.Nfiq1NotAssessable"
                )
                .ToArray();
            if (operational.Length > 0)
                return UnitResult.Failure(operational[0]);
        }
        return UnitResult.Success<DesktopError>();
    }
}
