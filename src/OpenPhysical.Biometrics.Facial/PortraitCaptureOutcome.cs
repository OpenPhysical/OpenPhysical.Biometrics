// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;

internal static class PortraitCaptureOutcome
{
    internal static PortraitPreparationResult MapFindings(
        this PortraitPreparationResult portrait,
        ImmutableArray<PortraitFinding> findings
    ) =>
        findings.IsEmpty
            ? portrait
            : portrait with
            {
                Status = PortraitStatus.ReviewRequired,
                Findings = [.. findings, .. portrait.Findings],
            };
}
