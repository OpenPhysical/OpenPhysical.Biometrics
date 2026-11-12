// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;

internal sealed record CaptureOriginalAssessment(
    CaptureImageQualityEvidence Evidence,
    Maybe<ScoredFingerprintImage> Assessment,
    Maybe<PreparedFingerprint> Extraction
)
{
    internal CaptureOriginalAssessment WithExtraction(PreparedFingerprint prepared) =>
        Assessment.HasValue && CaptureQualityReuse.Extracted(Assessment.Value, prepared)
            ? this with
            {
                Extraction = prepared,
            }
            : this;
}
