// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using OpenPhysical.Biometrics.Fingerprints;

internal static class CaptureQualityFailure
{
    internal static DesktopError From(CaptureImageQualityEvidence evidence)
    {
        var native = evidence
            .Diagnostics.Where(error =>
                error.Code == FingerprintErrorCodes.QualityImageNotAssessable
            )
            .ToArray();
        if (evidence.Nfiq2.HasNoValue && native.Length > 0)
            return native[0];
        var nfiq1 = evidence
            .Diagnostics.Where(error => error.Code == "Capture.Nfiq1NotAssessable")
            .ToArray();
        if (evidence.Nfiq1.Status != FingerprintQualityStatus.Assessed && nfiq1.Length > 0)
            return nfiq1[0];
        return new DesktopError(
            "Capture.QualityEvidenceMismatch",
            "An unavailable quality assessment lacks its exact engine diagnostic."
        );
    }
}
